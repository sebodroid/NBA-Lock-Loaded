using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NbaTracker.Data;
using NbaTracker.Data.Entities;
using NbaTracker.Worker.Models.OddsApi;

namespace NbaTracker.Worker.Services;

public class MlbSyncOrchestrator
{
    private readonly NbaTrackerDbContext _db;
    private readonly MlbStatsClient _mlbClient;
    private readonly OddsApiClient _oddsClient;
    private readonly IConfiguration _config;
    private readonly ILogger<MlbSyncOrchestrator> _logger;

    private static readonly TimeZoneInfo EasternTime =
        GameMatchingService.GetEasternTimeZone();

    public MlbSyncOrchestrator(
        NbaTrackerDbContext db,
        MlbStatsClient mlbClient,
        OddsApiClient oddsClient,
        IConfiguration config,
        ILogger<MlbSyncOrchestrator> logger)
    {
        _db = db;
        _mlbClient = mlbClient;
        _oddsClient = oddsClient;
        _config = config;
        _logger = logger;
    }

    public async Task RunDailySyncAsync(DateOnly date, CancellationToken ct)
    {
        _logger.LogInformation("[MLB] Starting daily sync for {Date}", date);

        var syncRun = new SyncRun
        {
            Sport = "MLB",
            SyncDate = date,
            StartedAt = DateTime.UtcNow,
            Status = SyncRunStatus.Running
        };
        _db.SyncRuns.Add(syncRun);
        await _db.SaveChangesAsync(ct);

        var errors = new List<string>();
        int gamesProcessed = 0;

        try
        {
            // Step a: Seed MLB teams if none exist yet
            await SeedTeamsIfEmptyAsync(ct);

            // Step b: Fetch MLB schedule for the date
            var mlbGames = await _mlbClient.GetGamesAsync(date, ct);
            _logger.LogInformation("[MLB] Fetched {Count} games for {Date}", mlbGames.Count, date);

            // Step c: Upsert each game
            foreach (var mlbGame in mlbGames)
            {
                try
                {
                    await UpsertGameAsync(mlbGame, date, ct);
                    await _db.SaveChangesAsync(ct);
                    gamesProcessed++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[MLB] Failed to upsert game {GamePk}", mlbGame.GamePk);
                    errors.Add($"MLB game {mlbGame.GamePk}: {ex.Message}");
                }
            }

            // Step d: Fetch odds — historical for past dates, live for today
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var oddsEvents = date < today
                ? await _oddsClient.GetHistoricalOddsAsync(date, ct, "baseball_mlb")
                : await _oddsClient.GetOddsAsync(ct, "baseball_mlb");

            _logger.LogInformation("[MLB] Fetched {Count} events from Odds API", oddsEvents.Count);

            // Build canonical key lookup from Odds API events
            var oddsLookup = new Dictionary<string, OddsApiEvent>(StringComparer.Ordinal);
            foreach (var evt in oddsEvents)
            {
                try
                {
                    var key = MlbGameMatchingService.BuildCanonicalKeyFromOddsApi(
                        evt.HomeTeam, evt.AwayTeam, evt.CommenceTime, EasternTime);
                    oddsLookup.TryAdd(key, evt);
                }
                catch (KeyNotFoundException)
                {
                    _logger.LogDebug("[MLB] Skipping unknown team: '{Home}' vs '{Away}'",
                        evt.HomeTeam, evt.AwayTeam);
                }
            }

            // Step e: Match games to odds and upsert lines
            var dbGames = await _db.Games
                .Include(g => g.HomeTeam)
                .Include(g => g.AwayTeam)
                .Include(g => g.GameLine)
                .Include(g => g.GameResult)
                .Where(g => g.GameDate == date
                         && g.Sport == "MLB"
                         && g.Status != "POSTPONED")
                .ToListAsync(ct);

            var primaryBookmaker   = _config["OddsApi:PrimaryBookmaker"]  ?? "fanduel";
            var fallbackBookmaker  = _config["OddsApi:FallbackBookmaker"] ?? "draftkings";

            foreach (var game in dbGames)
            {
                try
                {
                    var key = MlbGameMatchingService.BuildCanonicalKey(
                        game.HomeTeam.Abbreviation,
                        game.AwayTeam.Abbreviation,
                        date);

                    if (!oddsLookup.TryGetValue(key, out var oddsEvent))
                    {
                        _logger.LogDebug("[MLB] No odds match for game {GameId} ({Key})",
                            game.Id, key);
                        continue;
                    }

                    if (game.OddsApiGameId != oddsEvent.Id)
                    {
                        game.OddsApiGameId = oddsEvent.Id;
                        game.UpdatedAt = DateTime.UtcNow;
                    }

                    var bookmaker = OddsApiClient.SelectCanonicalBookmaker(
                        oddsEvent.Bookmakers, primaryBookmaker, fallbackBookmaker);

                    if (bookmaker is null)
                    {
                        _logger.LogDebug("[MLB] No canonical bookmaker for game {GameId}", game.Id);
                        await _db.SaveChangesAsync(ct);
                        continue;
                    }

                    // MLB uses h2h (moneyline) + totals — no spread market
                    var h2hMarket    = bookmaker.Markets.FirstOrDefault(m => m.Key == "h2h");
                    var totalsMarket = bookmaker.Markets.FirstOrDefault(m => m.Key == "totals");

                    decimal? total = null;
                    int? overOdds = null;
                    int? underOdds = null;
                    int? homeMoneyline = null;
                    int? awayMoneyline = null;

                    if (totalsMarket is not null && totalsMarket.Outcomes.Count >= 2)
                    {
                        var (extractedTotal, extractedOverOdds, extractedUnderOdds)
                            = OddsApiClient.ExtractTotal(totalsMarket);
                        total      = extractedTotal;
                        overOdds   = extractedOverOdds;
                        underOdds  = extractedUnderOdds;
                    }

                    if (h2hMarket is not null && h2hMarket.Outcomes.Count >= 2)
                    {
                        var homeOutcome = h2hMarket.Outcomes
                            .FirstOrDefault(o => string.Equals(
                                o.Name, oddsEvent.HomeTeam,
                                StringComparison.OrdinalIgnoreCase));
                        var awayOutcome = h2hMarket.Outcomes
                            .FirstOrDefault(o => string.Equals(
                                o.Name, oddsEvent.AwayTeam,
                                StringComparison.OrdinalIgnoreCase));

                        homeMoneyline = homeOutcome?.Price is decimal hp ? (int)Math.Round(hp) : null;
                        awayMoneyline = awayOutcome?.Price is decimal ap ? (int)Math.Round(ap) : null;
                    }

                    // MLB: favorite is team with lower (more negative) moneyline
                    int? favoriteTeamId = null;
                    if (homeMoneyline.HasValue && awayMoneyline.HasValue)
                    {
                        favoriteTeamId = homeMoneyline.Value <= awayMoneyline.Value
                            ? game.HomeTeamId
                            : game.AwayTeamId;
                    }

                    // Upsert GameLine — store moneyline in spread odds fields for MLB
                    // Spread stays null (MLB doesn't use run line by default)
                    if (game.GameLine is null)
                    {
                        _db.GameLines.Add(new GameLine
                        {
                            GameId         = game.Id,
                            Spread         = null,
                            FavoriteTeamId = favoriteTeamId,
                            Total          = total,
                            HomeSpreadOdds = homeMoneyline,
                            AwaySpreadOdds = awayMoneyline,
                            OverOdds       = overOdds,
                            UnderOdds      = underOdds,
                            Bookmaker      = bookmaker.Key,
                            LineTimestamp  = DateTime.UtcNow,
                            UpdatedAt      = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        game.GameLine.Spread         = null;
                        game.GameLine.FavoriteTeamId = favoriteTeamId;
                        game.GameLine.Total          = total;
                        game.GameLine.HomeSpreadOdds = homeMoneyline;
                        game.GameLine.AwaySpreadOdds = awayMoneyline;
                        game.GameLine.OverOdds       = overOdds;
                        game.GameLine.UnderOdds      = underOdds;
                        game.GameLine.Bookmaker      = bookmaker.Key;
                        game.GameLine.LineTimestamp  = DateTime.UtcNow;
                        game.GameLine.UpdatedAt      = DateTime.UtcNow;
                    }

                    // Calculate O/U for FINAL games
                   if (game.Status == "FINAL"
                        && game.GameLine?.Total.HasValue == true
                        && game.HomeScore.HasValue
                        && game.AwayScore.HasValue)
                    {
                        var ouResult = AtsOuCalculator.CalculateOu(
                            game.HomeScore.Value,
                            game.AwayScore.Value,
                            game.GameLine.Total.Value);

                        if (game.GameResult is null)
                        {
                            _db.GameResults.Add(new GameResult
                            {
                                GameId       = game.Id,
                                HomeAtsResult = null,   // no spread in MLB
                                AwayAtsResult = null,
                                OuResult     = ouResult,
                                ResolvedAt   = DateTime.UtcNow
                            });
                        }
                        else
                        {
                            game.GameResult.OuResult   = ouResult;
                            game.GameResult.ResolvedAt = DateTime.UtcNow;
                        }
                    }

                    await _db.SaveChangesAsync(ct);
                    _logger.LogDebug("[MLB] Lines saved for game {GameId}", game.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[MLB] Failed to process lines for game {GameId}", game.Id);
                    errors.Add($"Lines for game {game.Id}: {ex.Message}");
                }
            }

            syncRun.Status = errors.Count == 0
                ? SyncRunStatus.Success
                : SyncRunStatus.Partial;
            syncRun.GamesProcessed = gamesProcessed;
            syncRun.ErrorDetails   = errors.Count > 0
                ? JsonSerializer.Serialize(errors)
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MLB] Fatal error during sync for {Date}", date);
            syncRun.Status       = SyncRunStatus.Failed;
            syncRun.ErrorDetails = JsonSerializer.Serialize(new
            {
                fatal      = ex.Message,
                stackTrace = ex.StackTrace
            });
        }
        finally
        {
            syncRun.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(CancellationToken.None);
            _logger.LogInformation(
                "[MLB] Sync for {Date}: {Status}, {Games} games, {Errors} errors",
                date, syncRun.Status, syncRun.GamesProcessed, errors.Count);
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task SeedTeamsIfEmptyAsync(CancellationToken ct)
    {
        if (await _db.Teams.AnyAsync(t => t.Sport == "MLB", ct))
            return;

        _logger.LogInformation("[MLB] No MLB teams found — seeding");
        var mlbTeams = await _mlbClient.GetTeamsAsync(ct);

        foreach (var t in mlbTeams)
        {
            // MLB Stats API includes minor league teams — filter to majors only
            // Active MLB teams have a valid abbreviation and are in AL or NL
            if (string.IsNullOrWhiteSpace(t.Abbreviation)) continue;

            var existing = await _db.Teams.FirstOrDefaultAsync(
                x => x.Sport == "MLB" && x.NbaApiId == t.Id.ToString(), ct);

            if (existing is null)
            {
                _db.Teams.Add(new Team
                {
                    Sport        = "MLB",
                    NbaApiId     = t.Id.ToString(),   // reusing field as generic external ID
                    Name         = t.Name,
                    Abbreviation = t.Abbreviation,
                    Conference   = t.League?.Name,    // "American League" / "National League"
                    Division     = t.Division?.Name,
                    UpdatedAt    = DateTime.UtcNow
                });
            }
            else
            {
                existing.Name         = t.Name;
                existing.Abbreviation = t.Abbreviation;
                existing.Conference   = t.League?.Name;
                existing.Division     = t.Division?.Name;
                existing.UpdatedAt    = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("[MLB] Seeded MLB teams");
    }

    private async Task UpsertGameAsync(
        Models.MlbStats.MlbGame mlbGame, DateOnly date, CancellationToken ct)
    {
        // Map MLB abstract state → internal status
        string status = mlbGame.Status.AbstractGameState switch
        {
            "Final"    => "FINAL",
            "Live"     => "LIVE",
            "Preview"  => "SCHEDULED",
            _          => mlbGame.Status.DetailedState.Contains("Postponed")
                            ? "POSTPONED"
                            : "SCHEDULED"
        };

        // MLB season: April–October is current year
        string season = $"{date.Year}";

        var homeTeam = (await _db.Teams.FirstOrDefaultAsync(
        t => t.Sport == "MLB" && t.NbaApiId == mlbGame.Teams.Home.Team.Id.ToString(), ct))
        ?? throw new InvalidOperationException(
        $"MLB home team not found: {mlbGame.Teams.Home.Team.Id} ({mlbGame.Teams.Home.Team.Name})");

        var awayTeam = (await _db.Teams.FirstOrDefaultAsync(
            t => t.Sport == "MLB" && t.NbaApiId == mlbGame.Teams.Away.Team.Id.ToString(), ct))
            ?? throw new InvalidOperationException(
                $"MLB away team not found: {mlbGame.Teams.Away.Team.Id} ({mlbGame.Teams.Away.Team.Name})");

        var gameIdStr = mlbGame.GamePk.ToString();

        var existing = await _db.Games.FirstOrDefaultAsync(
            g => g.Sport == "MLB" && g.NbaGameId == gameIdStr, ct);

        if (existing is null)
        {
            _db.Games.Add(new Game
            {
                Sport       = "MLB",
                NbaGameId   = gameIdStr,
                GameDate    = date,
                HomeTeamId  = homeTeam.Id,
                AwayTeamId  = awayTeam.Id,
                Status      = status,
                Season      = season,
                HomeScore   = mlbGame.Teams.Home.Score,
                AwayScore   = mlbGame.Teams.Away.Score,
                CreatedAt   = DateTime.UtcNow,
                UpdatedAt   = DateTime.UtcNow
            });
        }
        else
        {
            existing.Status    = status;
            existing.HomeScore = mlbGame.Teams.Home.Score;
            existing.AwayScore = mlbGame.Teams.Away.Score;
            existing.UpdatedAt = DateTime.UtcNow;
        }
    }
}