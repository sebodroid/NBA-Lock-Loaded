using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NbaTracker.Data;
using NbaTracker.Data.Entities;
using NbaTracker.Worker.Models.BallDontLie;
using NbaTracker.Worker.Models.OddsApi;

namespace NbaTracker.Worker.Services;

public class SyncOrchestrator
{
    private readonly NbaTrackerDbContext _db;
    private readonly BallDontLieClient _bdlClient;
    private readonly OddsApiClient _oddsClient;
    private readonly IConfiguration _config;
    private readonly ILogger<SyncOrchestrator> _logger;
    private readonly SyncFileLogger _fileLogger;

    private static readonly TimeZoneInfo EasternTime = GameMatchingService.GetEasternTimeZone();

    public SyncOrchestrator(
        NbaTrackerDbContext db,
        BallDontLieClient bdlClient,
        OddsApiClient oddsClient,
        IConfiguration config,
        ILogger<SyncOrchestrator> logger,
        SyncFileLogger fileLogger)
    {
        _db = db;
        _bdlClient = bdlClient;
        _oddsClient = oddsClient;
        _config = config;
        _logger = logger;
        _fileLogger = fileLogger;
    }

    public async Task RunDailySyncAsync(DateOnly date, CancellationToken ct)
    {
        _logger.LogInformation("Starting daily sync for {Date}", date);
        _fileLogger.LogSyncStart(date);

        var syncRun = new SyncRun
        {
            StartedAt = DateTime.UtcNow,
            Status = SyncRunStatus.Running
        };
        _db.SyncRuns.Add(syncRun);
        await _db.SaveChangesAsync(ct);

        var errors = new List<string>();
        int gamesProcessed = 0;

        try
        {
            // Step a: Seed teams if Teams table is empty
            await SeedTeamsIfEmptyAsync(ct);

            // Step b: Fetch BDL games for the date
            // Determine season from date: month >= 10 → year, else year-1
            int season = date.Month >= 10 ? date.Year : date.Year - 1;
            var bdlGames = await _bdlClient.GetGamesAsync(season, date, ct);
            _logger.LogInformation("Fetched {Count} BDL games for {Date}", bdlGames.Count, date);

            // Step c: Upsert each BDL game
            foreach (var bdlGame in bdlGames)
            {
                try
                {
                    await UpsertGameAsync(bdlGame, ct);
                    await _db.SaveChangesAsync(ct);
                    gamesProcessed++;
                    var gameStatus = bdlGame.Postponed ? "POSTPONED"
                        : bdlGame.Status == "Final" ? "FINAL"
                        : (bdlGame.Status.Contains('Q') || bdlGame.Status.Contains("Halftime")) ? "LIVE"
                        : "SCHEDULED";
                    _fileLogger.LogGameWriteOk(date, bdlGame.Id.ToString(), gameStatus);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to upsert BDL game {GameId}", bdlGame.Id);
                    errors.Add($"BDL game {bdlGame.Id}: {ex.Message}");
                    _fileLogger.LogGameWriteFail(date, bdlGame.Id.ToString(), ex.Message);
                }
            }

            // Step d: Fetch Odds API lines — historical endpoint for past dates, live for today
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var oddsEvents = date < today
                ? await _oddsClient.GetHistoricalOddsAsync(date, ct)
                : await _oddsClient.GetOddsAsync(ct);
            _logger.LogInformation("Fetched {Count} events from Odds API", oddsEvents.Count);

            // Build canonical key lookup from Odds API events
            var oddsLookup = new Dictionary<string, OddsApiEvent>(StringComparer.Ordinal);
            foreach (var evt in oddsEvents)
            {
                try
                {
                    var key = GameMatchingService.BuildCanonicalKeyFromOddsApi(
                        season, evt.HomeTeam, evt.AwayTeam, evt.CommenceTime, EasternTime);
                    oddsLookup.TryAdd(key, evt);
                }
                catch (KeyNotFoundException)
                {
                    // Team name not in our dictionary — not an NBA regular-season team, skip
                    _logger.LogDebug("Skipping Odds API event: unknown team in '{Home}' vs '{Away}'",
                        evt.HomeTeam, evt.AwayTeam);
                }
            }

            // Step e: For each game in DB for that date (excluding postponed), match and upsert lines
            var dbGames = await _db.Games
                .Include(g => g.HomeTeam)
                .Include(g => g.AwayTeam)
                .Include(g => g.GameLine)
                .Include(g => g.GameResult)
                .Where(g => g.GameDate == date && g.Status != "POSTPONED")
                .ToListAsync(ct);

            var primaryBookmaker = _config["OddsApi:PrimaryBookmaker"] ?? "fanduel";
            var fallbackBookmaker = _config["OddsApi:FallbackBookmaker"] ?? "hardrockbet";

            foreach (var game in dbGames)
            {
                try
                {
                    var bdlKey = GameMatchingService.BuildCanonicalKeyFromBdl(
                        season,
                        game.HomeTeam.Abbreviation,
                        game.AwayTeam.Abbreviation,
                        date);

                    if (!oddsLookup.TryGetValue(bdlKey, out var oddsEvent))
                    {
                        _logger.LogDebug("No Odds API match for game {GameId} ({Key})", game.Id, bdlKey);
                        continue;
                    }

                    // Store OddsApiGameId on the game entity
                    if (game.OddsApiGameId != oddsEvent.Id)
                    {
                        game.OddsApiGameId = oddsEvent.Id;
                        game.UpdatedAt = DateTime.UtcNow;
                    }

                    // Select canonical bookmaker
                    var bookmaker = OddsApiClient.SelectCanonicalBookmaker(
                        oddsEvent.Bookmakers, primaryBookmaker, fallbackBookmaker);

                    if (bookmaker is null)
                    {
                        _logger.LogDebug("No canonical bookmaker found for game {GameId}", game.Id);
                        await _db.SaveChangesAsync(ct);
                        continue;
                    }

                    var spreadsMarket = bookmaker.Markets.FirstOrDefault(m => m.Key == "spreads");
                    var totalsMarket = bookmaker.Markets.FirstOrDefault(m => m.Key == "totals");

                    decimal? spread = null;
                    int? favoriteTeamId = null;
                    int? homeSpreadOdds = null;
                    int? awaySpreadOdds = null;
                    decimal? total = null;
                    int? overOdds = null;
                    int? underOdds = null;

                    if (spreadsMarket is not null && spreadsMarket.Outcomes.Count >= 2)
                    {
                        var (extractedSpread, favoriteTeamName, favOdds, underdogOdds)
                            = OddsApiClient.ExtractSpread(spreadsMarket);

                        spread = extractedSpread;
                        homeSpreadOdds = oddsEvent.HomeTeam == favoriteTeamName ? favOdds : underdogOdds;
                        awaySpreadOdds = oddsEvent.HomeTeam == favoriteTeamName ? underdogOdds : favOdds;

                        // Resolve FavoriteTeamId: Odds API full name → abbreviation → Team in DB
                        if (GameMatchingService.TeamNameToAbbreviation.TryGetValue(
                                favoriteTeamName, out var favoriteAbbr))
                        {
                            var favoriteTeam = await _db.Teams
                                .FirstOrDefaultAsync(t => t.Abbreviation == favoriteAbbr, ct);
                            favoriteTeamId = favoriteTeam?.Id;
                        }
                    }

                    if (totalsMarket is not null && totalsMarket.Outcomes.Count >= 2)
                    {
                        var (extractedTotal, extractedOverOdds, extractedUnderOdds)
                            = OddsApiClient.ExtractTotal(totalsMarket);
                        total = extractedTotal;
                        overOdds = extractedOverOdds;
                        underOdds = extractedUnderOdds;
                    }

                    // Upsert GameLine
                    if (game.GameLine is null)
                    {
                        var gameLine = new GameLine
                        {
                            GameId = game.Id,
                            Spread = spread,
                            FavoriteTeamId = favoriteTeamId,
                            Total = total,
                            HomeSpreadOdds = homeSpreadOdds,
                            AwaySpreadOdds = awaySpreadOdds,
                            OverOdds = overOdds,
                            UnderOdds = underOdds,
                            Bookmaker = bookmaker.Key,
                            LineTimestamp = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _db.GameLines.Add(gameLine);
                        game.GameLine = gameLine;
                    }
                    else
                    {
                        game.GameLine.Spread = spread;
                        game.GameLine.FavoriteTeamId = favoriteTeamId;
                        game.GameLine.Total = total;
                        game.GameLine.HomeSpreadOdds = homeSpreadOdds;
                        game.GameLine.AwaySpreadOdds = awaySpreadOdds;
                        game.GameLine.OverOdds = overOdds;
                        game.GameLine.UnderOdds = underOdds;
                        game.GameLine.Bookmaker = bookmaker.Key;
                        game.GameLine.LineTimestamp = DateTime.UtcNow;
                        game.GameLine.UpdatedAt = DateTime.UtcNow;
                    }

                    // Calculate ATS/OU for FINAL games with sufficient data
                    bool resultCalculated = false;
                    string logHomeAts = "", logAwayAts = "", logOu = "";
                    if (game.Status == "FINAL"
                        && game.GameLine.Spread.HasValue
                        && game.GameLine.Total.HasValue
                        && game.GameLine.FavoriteTeamId.HasValue
                        && game.HomeScore.HasValue
                        && game.AwayScore.HasValue)
                    {
                        int favTeamId = game.GameLine.FavoriteTeamId.Value;
                        var favoriteAtsResult = AtsOuCalculator.CalculateFavoriteAts(
                            game.HomeScore.Value,
                            game.AwayScore.Value,
                            game.GameLine.Spread.Value,
                            favTeamId,
                            game.HomeTeamId);

                        var (homeAts, awayAts) = AtsOuCalculator.DeriveBothSides(
                            favoriteAtsResult, favTeamId, game.HomeTeamId);

                        var ouResult = AtsOuCalculator.CalculateOu(
                            game.HomeScore.Value,
                            game.AwayScore.Value,
                            game.GameLine.Total.Value);

                        if (game.GameResult is null)
                        {
                            var gameResult = new GameResult
                            {
                                GameId = game.Id,
                                HomeAtsResult = homeAts,
                                AwayAtsResult = awayAts,
                                OuResult = ouResult,
                                ResolvedAt = DateTime.UtcNow
                            };
                            _db.GameResults.Add(gameResult);
                        }
                        else
                        {
                            game.GameResult.HomeAtsResult = homeAts;
                            game.GameResult.AwayAtsResult = awayAts;
                            game.GameResult.OuResult = ouResult;
                            game.GameResult.ResolvedAt = DateTime.UtcNow;
                        }

                        resultCalculated = true;
                        logHomeAts = homeAts.ToString();
                        logAwayAts = awayAts.ToString();
                        logOu = ouResult.ToString();
                    }

                    await _db.SaveChangesAsync(ct);
                    _fileLogger.LogLineWriteOk(date, game.Id, game.GameLine.Spread, game.GameLine.Total, game.GameLine.Bookmaker);
                    if (resultCalculated)
                        _fileLogger.LogResultWriteOk(date, game.Id, logHomeAts, logAwayAts, logOu);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process lines for game {GameId}", game.Id);
                    errors.Add($"Lines for game {game.Id}: {ex.Message}");
                    _fileLogger.LogLineWriteFail(date, game.Id, ex.Message);
                }
            }

            syncRun.Status = errors.Count == 0 ? SyncRunStatus.Success : SyncRunStatus.Partial;
            syncRun.GamesProcessed = gamesProcessed;
            syncRun.ErrorDetails = errors.Count > 0 ? JsonSerializer.Serialize(errors) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error during sync for {Date}", date);
            syncRun.Status = SyncRunStatus.Failed;
            syncRun.ErrorDetails = JsonSerializer.Serialize(new
            {
                fatal = ex.Message,
                stackTrace = ex.StackTrace
            });
        }
        finally
        {
            syncRun.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(CancellationToken.None); // NEVER stoppingToken here
            _logger.LogInformation(
                "Sync for {Date} completed: {Status}, {GamesProcessed} games processed, {ErrorCount} errors",
                date, syncRun.Status, syncRun.GamesProcessed, errors.Count);
            _fileLogger.LogSyncComplete(
                date,
                syncRun.Status.ToString(),
                syncRun.GamesProcessed ?? 0,
                errors.Count,
                syncRun.ErrorDetails);
        }
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    private async Task SeedTeamsIfEmptyAsync(CancellationToken ct)
    {
        if (await _db.Teams.AnyAsync(ct))
            return;

        _logger.LogInformation("Teams table empty — seeding from BallDontLie");
        var bdlTeams = await _bdlClient.GetTeamsAsync(ct);

        // Filter to current NBA teams only — BallDontLie returns all historical teams;
        // active teams always have a valid conference (East or West)
        var activeTeams = bdlTeams
            .Where(t => t.Conference == "East" || t.Conference == "West")
            .ToList();

        _logger.LogInformation("Filtered to {Count} active teams (from {Total} total)", activeTeams.Count, bdlTeams.Count);

        foreach (var bdlTeam in activeTeams)
        {
            var existing = await _db.Teams
                .FirstOrDefaultAsync(t => t.NbaApiId == bdlTeam.Id.ToString(), ct);

            if (existing is null)
            {
                _db.Teams.Add(new Team
                {
                    NbaApiId = bdlTeam.Id.ToString(),
                    Name = bdlTeam.FullName,
                    Abbreviation = bdlTeam.Abbreviation,
                    Conference = bdlTeam.Conference,
                    Division = bdlTeam.Division,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.Name = bdlTeam.FullName;
                existing.Abbreviation = bdlTeam.Abbreviation;
                existing.Conference = bdlTeam.Conference;
                existing.Division = bdlTeam.Division;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} teams", activeTeams.Count);
    }

    private async Task UpsertGameAsync(BdlGame bdlGame, CancellationToken ct)
    {
        // Map BDL status string → internal status string
        string status;
        if (bdlGame.Postponed)
            status = "POSTPONED";
        else if (bdlGame.Status == "Final")
            status = "FINAL";
        else if (bdlGame.Status.Contains('Q') || bdlGame.Status.Contains("Halftime"))
            status = "LIVE";
        else
            status = "SCHEDULED";

        // Season string: "2025-26"
        string seasonStr = $"{bdlGame.Season}-{(bdlGame.Season + 1) % 100:D2}";

        // Parse game date from BDL "2025-11-01" string
        var gameDate = DateOnly.Parse(bdlGame.Date);

        // Look up team IDs by NbaApiId
        var homeTeam = await _db.Teams.FirstAsync(
            t => t.NbaApiId == bdlGame.HomeTeam.Id.ToString(), ct);
        var awayTeam = await _db.Teams.FirstAsync(
            t => t.NbaApiId == bdlGame.VisitorTeam.Id.ToString(), ct);

        var existing = await _db.Games.FirstOrDefaultAsync(
            g => g.NbaGameId == bdlGame.Id.ToString(), ct);

        if (existing is null)
        {
            _db.Games.Add(new Game
            {
                NbaGameId = bdlGame.Id.ToString(),
                GameDate = gameDate,
                HomeTeamId = homeTeam.Id,
                AwayTeamId = awayTeam.Id,
                Status = status,
                Season = seasonStr,
                HomeScore = bdlGame.HomeTeamScore,
                AwayScore = bdlGame.VisitorTeamScore,
                WentToOvertime = bdlGame.Period > 4 ? true : null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Status = status;
            existing.HomeScore = bdlGame.HomeTeamScore;
            existing.AwayScore = bdlGame.VisitorTeamScore;
            existing.WentToOvertime = bdlGame.Period > 4 ? true : existing.WentToOvertime;
            existing.UpdatedAt = DateTime.UtcNow;
        }
    }
}
