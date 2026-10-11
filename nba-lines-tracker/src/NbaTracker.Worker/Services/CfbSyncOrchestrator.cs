using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NbaTracker.Data;
using NbaTracker.Data.Entities;
using NbaTracker.Worker.Models.NflStats;
using NbaTracker.Worker.Models.OddsApi;

namespace NbaTracker.Worker.Services;

public class CfbSyncOrchestrator
{
    private const string Sport = "CFB";
    private const string HighlightlyLeague = "NCAA";

    private const bool ScoreCurrentIsHomeFirst = true;

    private static readonly string[] OddsSportKeys =
        ["americanfootball_ncaaf", "americanfootball_ncaaf_fcs"];

    private const string LineMarkets = "spreads,totals";

    private static readonly string[] ExtraBookmakers = ["draftkings", "betmgm", "williamhill_us"];

    private static readonly TimeSpan TeamPropsFreshFor = TimeSpan.FromHours(3);

    private static readonly TimeZoneInfo EasternTime = GameMatchingService.GetEasternTimeZone();

    private readonly NbaTrackerDbContext _db;
    private readonly NflStatsClient _statsClient;
    private readonly OddsApiClient _oddsClient;
    private readonly string[] _bookmakerPriority;
    private readonly ILogger<CfbSyncOrchestrator> _logger;

    public CfbSyncOrchestrator(
        NbaTrackerDbContext db,
        NflStatsClient statsClient,
        OddsApiClient oddsClient,
        IConfiguration config,
        ILogger<CfbSyncOrchestrator> logger)
    {
        _db = db;
        _statsClient = statsClient;
        _oddsClient = oddsClient;
        _logger = logger;

        _bookmakerPriority = new[]
            {
                config["OddsApi:PrimaryBookmaker"] ?? "fanduel",
                config["OddsApi:FallbackBookmaker"] ?? "hardrockbet",
            }
            .Concat(ExtraBookmakers)
            .Distinct()
            .ToArray();
    }

    public async Task RunDailySyncAsync(DateOnly date, CancellationToken ct)
    {
        _logger.LogInformation("[CFB] Starting daily sync for {Date}", date);

        var afterDay = DateTime.SpecifyKind(date.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var historicalPassDone = await _db.SyncRuns.AnyAsync(
            r => r.Sport == Sport && r.SyncDate == date
              && r.Status == SyncRunStatus.Success && r.StartedAt >= afterDay, ct);

        var syncRun = new SyncRun
        {
            Sport = Sport,
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
            var matches = await _statsClient.GetGamesAsync(date, ct, HighlightlyLeague);
            _logger.LogInformation("[CFB] Fetched {Count} NCAA matches for {Date}", matches.Count, date);

            var matchIds = matches.Select(m => m.Id.ToString()).ToList();
            var games = await _db.Games
                .Include(g => g.HomeTeam)
                .Include(g => g.AwayTeam)
                .Include(g => g.GameLine)
                .Include(g => g.GameResult)
                .Where(g => g.Sport == Sport && matchIds.Contains(g.NbaGameId))
                .ToListAsync(ct);
            var gamesByMatchId = games.ToDictionary(g => g.NbaGameId);

            foreach (var match in matches)
            {
                if (!gamesByMatchId.TryGetValue(match.Id.ToString(), out var tracked)) continue;
                ApplyMatchState(tracked, match, date, tracked.HomeTeam.Name, tracked.AwayTeam.Name);
                ResolveResult(tracked);
                gamesProcessed++;
            }
            await _db.SaveChangesAsync(ct);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            bool needsOddsFetch = matches.Count > 0 && (date >= today || !historicalPassDone);

            if (!needsOddsFetch)
            {
                _logger.LogDebug("[CFB] Skipping odds fetch for {Date} — historical pass already done", date);
            }
            else
            {
                var oddsEvents = await FetchOddsEventsAsync(date, today, errors, ct);
                _logger.LogInformation("[CFB] Fetched {Count} Odds API events for {Date}", oddsEvents.Count, date);

                var matchedEventIds = new HashSet<string>();
                var propTargets = new List<(Game Game, string SportKey, bool Swapped)>();
                int withLines = 0, withoutOdds = 0;

                foreach (var match in matches)
                {
                    try
                    {
                        gamesByMatchId.TryGetValue(match.Id.ToString(), out var game);

                        var candidates = CfbGameMatchingService.FindCandidates(
                            match.HomeTeam.DisplayName, match.AwayTeam.DisplayName,
                            date, oddsEvents, EasternTime);

                        if (candidates.Count > 1)
                        {
                            _logger.LogWarning(
                                "[CFB] {Count} Odds API events match '{Away}' @ '{Home}' on {Date} — skipping as ambiguous",
                                candidates.Count, match.AwayTeam.DisplayName, match.HomeTeam.DisplayName, date);
                            continue;
                        }

                        if (candidates.Count == 0)
                        {
                            if (game is null) withoutOdds++;
                            continue;
                        }

                        var (oddsEvent, swapped) = candidates[0];
                        matchedEventIds.Add(oddsEvent.Id);

                        var bookmaker = PickBookmaker(oddsEvent);
                        if (bookmaker is null)
                        {
                            _logger.LogDebug(
                                "[CFB] Odds event {EventId} ({Away} @ {Home}) has no spreads/totals from any preferred bookmaker",
                                oddsEvent.Id, oddsEvent.AwayTeam, oddsEvent.HomeTeam);
                            if (game is null) withoutOdds++;
                            continue;
                        }

                        if (game is null)
                        {
                            game = await CreateGameAsync(match, date, ct);
                            gamesByMatchId[game.NbaGameId] = game;
                            gamesProcessed++;
                        }

                        if (game.OddsApiGameId != oddsEvent.Id)
                        {
                            game.OddsApiGameId = oddsEvent.Id;
                            game.UpdatedAt = DateTime.UtcNow;
                        }

                        UpsertLine(game, oddsEvent, swapped, bookmaker);
                        ResolveResult(game);

                        await _db.SaveChangesAsync(ct);
                        withLines++;
                        _logger.LogDebug("[CFB] Lines saved for game {GameId}", game.Id);

                        if (date == today && game.Status == "SCHEDULED")
                            propTargets.Add((game, oddsEvent.SportKey, swapped));
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogWarning(ex, "[CFB] Failed to process match {MatchId}", match.Id);
                        errors.Add($"CFB match {match.Id}: {ex.Message}");
                    }
                }

                _logger.LogInformation(
                    "[CFB] {Date}: {Matched} games with lines, {Skipped} matches skipped (no FBS/FCS odds)",
                    date, withLines, withoutOdds);

                foreach (var (propGame, sportKey, swapped) in propTargets)
                {
                    try
                    {
                        await SyncTeamPropsAsync(propGame, sportKey, swapped, ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogWarning(ex, "[CFB] Failed to sync team props for game {GameId}", propGame.Id);
                        errors.Add($"Team props for game {propGame.Id}: {ex.Message}");
                    }
                }

                var unmatched = oddsEvents
                    .Where(e => !matchedEventIds.Contains(e.Id)
                             && CfbGameMatchingService.EasternDate(e.CommenceTime, EasternTime) == date)
                    .Select(e => $"{e.AwayTeam} @ {e.HomeTeam}")
                    .ToList();
                if (unmatched.Count > 0)
                {
                    _logger.LogWarning(
                        "[CFB] {Count} Odds API events on {Date} had no Highlightly match (add a name alias if a real game): {Events}",
                        unmatched.Count, date, string.Join("; ", unmatched));
                }
            }

            syncRun.Status = errors.Count == 0
                ? SyncRunStatus.Success
                : SyncRunStatus.Partial;
            syncRun.GamesProcessed = gamesProcessed;
            syncRun.ErrorDetails = errors.Count > 0
                ? JsonSerializer.Serialize(errors)
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CFB] Fatal error during sync for {Date}", date);
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
            await _db.SaveChangesAsync(CancellationToken.None);
            _logger.LogInformation(
                "[CFB] Sync for {Date}: {Status}, {Games} games, {Errors} errors",
                date, syncRun.Status, syncRun.GamesProcessed, errors.Count);
        }
    }

    private async Task<List<OddsApiEvent>> FetchOddsEventsAsync(
        DateOnly date, DateOnly today, List<string> errors, CancellationToken ct)
    {
        var events = new Dictionary<string, OddsApiEvent>();

        foreach (var sportKey in OddsSportKeys)
        {
            try
            {
                var fetched = date < today
                    ? await _oddsClient.GetHistoricalOddsAsync(date, ct, sportKey, _bookmakerPriority, LineMarkets)
                    : await _oddsClient.GetOddsAsync(ct, sportKey, _bookmakerPriority, LineMarkets);

                foreach (var evt in fetched) events.TryAdd(evt.Id, evt);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "[CFB] Odds fetch failed for {SportKey} on {Date}", sportKey, date);
                errors.Add($"Odds fetch ({sportKey}): {ex.Message}");
            }
        }

        return events.Values.ToList();
    }

    private OddsApiBookmaker? PickBookmaker(OddsApiEvent evt)
    {
        foreach (var key in _bookmakerPriority)
        {
            var book = evt.Bookmakers.FirstOrDefault(b =>
                b.Key == key && b.Markets.Any(m => m.Key is "spreads" or "totals"));
            if (book is not null) return book;
        }
        return null;
    }

    private async Task SyncTeamPropsAsync(Game game, string sportKey, bool swapped, CancellationToken ct)
    {
        if (game.OddsApiGameId is null) return;

        var lastFetched = await _db.TeamPropLines
            .Where(l => l.GameId == game.Id)
            .Select(l => (DateTime?)l.UpdatedAt)
            .MaxAsync(ct);
        if (lastFetched is not null && DateTime.UtcNow - lastFetched < TeamPropsFreshFor)
        {
            _logger.LogDebug("[CFB] Team props for game {GameId} are fresh — skipping fetch", game.Id);
            return;
        }

        var propsEvent = await _oddsClient.GetEventPropsAsync(
            sportKey, game.OddsApiGameId, TeamPropMarkets.Keys, _bookmakerPriority, ct);
        if (propsEvent is null)
        {
            _logger.LogInformation("[CFB] No team props response for game {GameId}", game.Id);
            return;
        }

        var lines = CfbTeamProps.BuildLines(game, propsEvent, swapped, _bookmakerPriority);
        if (lines.Count == 0)
        {
            _logger.LogInformation("[CFB] No team props posted for game {GameId}", game.Id);
            return;
        }

        var existing = await _db.TeamPropLines.Where(l => l.GameId == game.Id).ToListAsync(ct);
        _db.TeamPropLines.RemoveRange(existing);
        _db.TeamPropLines.AddRange(lines);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[CFB] Team props for game {GameId}: {Markets} markets, {Outcomes} outcomes",
            game.Id, lines.Select(l => l.MarketKey).Distinct().Count(), lines.Count);
    }

    private void UpsertLine(Game game, OddsApiEvent oddsEvent, bool swapped, OddsApiBookmaker bookmaker)
    {
        var built = BuildLine(game, oddsEvent, swapped, bookmaker);

        if (built.Spread.HasValue && !built.FavoriteTeamId.HasValue)
        {
            _logger.LogWarning(
                "[CFB] Spread favorite matches neither team of Odds event {EventId} — favorite left unset",
                oddsEvent.Id);
        }

        if (game.GameLine is null)
        {
            _db.GameLines.Add(built);
            game.GameLine = built;
        }
        else
        {
            game.GameLine.Spread         = built.Spread;
            game.GameLine.FavoriteTeamId = built.FavoriteTeamId;
            game.GameLine.Total          = built.Total;
            game.GameLine.HomeSpreadOdds = built.HomeSpreadOdds;
            game.GameLine.AwaySpreadOdds = built.AwaySpreadOdds;
            game.GameLine.OverOdds       = built.OverOdds;
            game.GameLine.UnderOdds      = built.UnderOdds;
            game.GameLine.Bookmaker      = built.Bookmaker;
            game.GameLine.LineTimestamp  = built.LineTimestamp;
            game.GameLine.UpdatedAt      = built.UpdatedAt;
        }
    }

    public static GameLine BuildLine(
        Game game, OddsApiEvent oddsEvent, bool swapped, OddsApiBookmaker bookmaker)
    {
        var spreadsMarket = bookmaker.Markets.FirstOrDefault(m => m.Key == "spreads");
        var totalsMarket  = bookmaker.Markets.FirstOrDefault(m => m.Key == "totals");

        var line = new GameLine
        {
            GameId        = game.Id,
            Bookmaker     = bookmaker.Key,
            LineTimestamp = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow
        };

        if (spreadsMarket is not null && spreadsMarket.Outcomes.Count >= 2)
        {
            var (spread, favoriteName, favoriteOdds, underdogOdds)
                = OddsApiClient.ExtractSpread(spreadsMarket);
            line.Spread = spread;

            bool favoriteIsOddsHome = string.Equals(favoriteName, oddsEvent.HomeTeam, StringComparison.Ordinal);
            bool favoriteIsOddsAway = string.Equals(favoriteName, oddsEvent.AwayTeam, StringComparison.Ordinal);

            if (favoriteIsOddsHome || favoriteIsOddsAway)
            {
                bool favoriteIsGameHome = favoriteIsOddsHome != swapped;
                line.FavoriteTeamId = favoriteIsGameHome ? game.HomeTeamId : game.AwayTeamId;
                line.HomeSpreadOdds = favoriteIsGameHome ? favoriteOdds : underdogOdds;
                line.AwaySpreadOdds = favoriteIsGameHome ? underdogOdds : favoriteOdds;
            }
        }

        if (totalsMarket is not null && totalsMarket.Outcomes.Count >= 2)
        {
            var (total, overOdds, underOdds) = OddsApiClient.ExtractTotal(totalsMarket);
            line.Total     = total;
            line.OverOdds  = overOdds;
            line.UnderOdds = underOdds;
        }

        return line;
    }

    private void ResolveResult(Game game)
    {
        var line = game.GameLine;
        if (game.Status != "FINAL"
            || line is null
            || !line.Spread.HasValue
            || !line.Total.HasValue
            || !line.FavoriteTeamId.HasValue
            || !game.HomeScore.HasValue
            || !game.AwayScore.HasValue)
            return;

        int favTeamId = line.FavoriteTeamId.Value;
        var favoriteAtsResult = AtsOuCalculator.CalculateFavoriteAts(
            game.HomeScore.Value, game.AwayScore.Value, line.Spread.Value, favTeamId, game.HomeTeamId);

        var (homeAts, awayAts) = AtsOuCalculator.DeriveBothSides(
            favoriteAtsResult, favTeamId, game.HomeTeamId);

        var ouResult = AtsOuCalculator.CalculateOu(
            game.HomeScore.Value, game.AwayScore.Value, line.Total.Value);

        if (game.GameResult is null)
        {
            var result = new GameResult
            {
                GameId        = game.Id,
                HomeAtsResult = homeAts,
                AwayAtsResult = awayAts,
                OuResult      = ouResult,
                ResolvedAt    = DateTime.UtcNow
            };
            _db.GameResults.Add(result);
            game.GameResult = result;
        }
        else if (game.GameResult.HomeAtsResult != homeAts
              || game.GameResult.AwayAtsResult != awayAts
              || game.GameResult.OuResult != ouResult)
        {
            game.GameResult.HomeAtsResult = homeAts;
            game.GameResult.AwayAtsResult = awayAts;
            game.GameResult.OuResult      = ouResult;
            game.GameResult.ResolvedAt    = DateTime.UtcNow;
        }
    }

    private async Task<Game> CreateGameAsync(HighlightlyMatch match, DateOnly date, CancellationToken ct)
    {
        var homeTeam = await GetOrCreateTeamAsync(match.HomeTeam, ct);
        var awayTeam = await GetOrCreateTeamAsync(match.AwayTeam, ct);

        var game = new Game
        {
            Sport      = Sport,
            NbaGameId  = match.Id.ToString(),
            GameDate   = date,
            HomeTeamId = homeTeam.Id,
            AwayTeamId = awayTeam.Id,
            Season     = match.Season.ToString(),
            CreatedAt  = DateTime.UtcNow
        };
        ApplyMatchState(game, match, date, homeTeam.Name, awayTeam.Name);
        _db.Games.Add(game);

        await _db.SaveChangesAsync(ct);
        return game;
    }

    private async Task<Team> GetOrCreateTeamAsync(HighlightlyTeamRef teamRef, CancellationToken ct)
    {
        var externalId = teamRef.Id.ToString();
        var team = await _db.Teams.FirstOrDefaultAsync(
            t => t.Sport == Sport && t.NbaApiId == externalId, ct);

        if (team is null)
        {
            team = new Team
            {
                Sport        = Sport,
                NbaApiId     = externalId,
                Name         = teamRef.DisplayName,
                Abbreviation = teamRef.Abbreviation,
                UpdatedAt    = DateTime.UtcNow
            };
            _db.Teams.Add(team);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("[CFB] Added team {Name} ({Abbr})", team.Name, team.Abbreviation);
        }
        else if (team.Name != teamRef.DisplayName || team.Abbreviation != teamRef.Abbreviation)
        {
            team.Name         = teamRef.DisplayName;
            team.Abbreviation = teamRef.Abbreviation;
            team.UpdatedAt    = DateTime.UtcNow;
        }

        return team;
    }

    private void ApplyMatchState(
        Game game, HighlightlyMatch match, DateOnly date, string? homeTeamName, string? awayTeamName)
    {
        var status = match.State.Description switch
        {
            "Scheduled"   => "SCHEDULED",
            "In progress" => "LIVE",
            "Finished"    => "FINAL",
            _             => "POSTPONED"
        };

        var (homeScore, awayScore) = ParseScore(
            match.State.Score?.Current, match.Id, homeTeamName, awayTeamName);
        bool overtime = !string.IsNullOrEmpty(match.State.Score?.FirstOvertimePeriod);

        bool changed = game.Status != status
            || game.HomeScore != homeScore
            || game.AwayScore != awayScore
            || (overtime && game.WentToOvertime != true)
            || game.GameDate != date;

        if (game.GameDate != date)
        {
            _logger.LogInformation("[CFB] Correcting GameDate for match {MatchId}: {Old} -> {New}",
                match.Id, game.GameDate, date);
            game.GameDate = date;
        }

        game.Status    = status;
        game.HomeScore = homeScore;
        game.AwayScore = awayScore;
        if (overtime) game.WentToOvertime = true;

        if (changed) game.UpdatedAt = DateTime.UtcNow;
    }

    private (int? Home, int? Away) ParseScore(
        string? current, long matchId, string? homeTeamName, string? awayTeamName)
    {
        if (string.IsNullOrWhiteSpace(current)) return (null, null);

        var parts = current.Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !int.TryParse(parts[0], out var first)
            || !int.TryParse(parts[1], out var second))
        {
            _logger.LogWarning("[CFB] Unparseable score '{Score}' for match {MatchId}", current, matchId);
            return (null, null);
        }

        var (home, away) = ScoreCurrentIsHomeFirst ? (first, second) : (second, first);

        _logger.LogDebug(
            "[CFB] Score parsed — match {MatchId} raw='{Raw}' → {HomeTeam}={Home} {AwayTeam}={Away}",
            matchId, current, homeTeamName, home, awayTeamName, away);

        return (home, away);
    }
}
