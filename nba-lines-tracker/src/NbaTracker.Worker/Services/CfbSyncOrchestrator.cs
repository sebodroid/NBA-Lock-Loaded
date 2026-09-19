using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NbaTracker.Data;
using NbaTracker.Data.Entities;
using NbaTracker.Worker.Models.NflStats;
using NbaTracker.Worker.Models.OddsApi;

namespace NbaTracker.Worker.Services;

/// <summary>
/// College football sync. Follows NflSyncOrchestrator's shape, with differences forced
/// by the data: Highlightly's NCAA feed covers every division (~150 games on a Saturday,
/// ~755 teams) while only FBS/FCS games have betting lines, so a game is ingested only
/// once it matches an Odds API event. Teams are seeded lazily from those matched games
/// instead of bulk-loading the full team list. Box scores and player props are not
/// captured — nothing consumes them for CFB, and college box scores are large.
/// </summary>
public class CfbSyncOrchestrator
{
    private const string Sport = "CFB";
    private const string HighlightlyLeague = "NCAA";

    // Confirmed against real results (Indiana 56-22 Oregon, Indiana 27-21 Miami — Indiana
    // is Highlightly's homeTeam in both and its score is listed first).
    private const bool ScoreCurrentIsHomeFirst = true;

    // FBS and FCS are separate Odds API sport keys; FBS-vs-FCS games can appear under either.
    private static readonly string[] OddsSportKeys =
        ["americanfootball_ncaaf", "americanfootball_ncaaf_fcs"];

    // Checked after the configured primary/fallback pair. College games get lines from a
    // patchier set of books than the NFL, and asking for more costs no extra API credits.
    private static readonly string[] ExtraBookmakers = ["draftkings", "betmgm", "williamhill_us"];

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

        // Read before this run's own SyncRun row is added. A run that started on or after
        // UTC midnight following the game date is one that took the historical-odds path
        // (see FetchOddsEventsAsync), which is the definitive line snapshot for the day.
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

            // Refresh status/score for games we already track, and settle any that just
            // went final from their stored line. Neither needs the Odds API, so a game
            // that finishes after its date's odds were last fetched still gets its result.
            foreach (var match in matches)
            {
                if (!gamesByMatchId.TryGetValue(match.Id.ToString(), out var tracked)) continue;
                ApplyMatchState(tracked, match, date, tracked.HomeTeam.Name, tracked.AwayTeam.Name);
                ResolveResult(tracked);
                gamesProcessed++;
            }
            await _db.SaveChangesAsync(ct);

            // Odds are the expensive call (two sport keys, and a historical request costs
            // extra credits), so a past date is fetched until ONE post-day pass has
            // succeeded and never again — rather than until every game is "settled",
            // which some games never reach (e.g. no posted total). That single pass is
            // also the only chance to discover games: most of a day's matches are
            // non-betting divisions that are never ingested, so the DB can't say what's new.
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
                            // The expected outcome for non-FBS/FCS games; for a tracked game
                            // it just means no line update this pass.
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

                // A betting event with no Highlightly match usually means a name the
                // matcher doesn't know yet — surface it so an alias can be added.
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

    // -------------------------------------------------------------------------
    // Odds
    // -------------------------------------------------------------------------

    /// <summary>
    /// Pulls FBS and FCS events. One key failing (e.g. FCS quiet in a given week) must
    /// not discard the other, so failures are recorded as errors — which marks the run
    /// Partial and makes the date get retried — rather than aborting the sync.
    /// </summary>
    private async Task<List<OddsApiEvent>> FetchOddsEventsAsync(
        DateOnly date, DateOnly today, List<string> errors, CancellationToken ct)
    {
        var events = new Dictionary<string, OddsApiEvent>();

        foreach (var sportKey in OddsSportKeys)
        {
            try
            {
                var fetched = date < today
                    ? await _oddsClient.GetHistoricalOddsAsync(date, ct, sportKey, _bookmakerPriority)
                    : await _oddsClient.GetOddsAsync(ct, sportKey, _bookmakerPriority);

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

    // First preferred bookmaker that actually posted a spread or total for this event —
    // an event can list a book that only offers a moneyline, which is no use here.
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

    /// <summary>
    /// Builds the game's line from an Odds API bookmaker. Spread and odds are mapped to
    /// the game's home/away by TEAM identity: for neutral-site and bowl games the Odds
    /// API's "home" team is frequently Highlightly's "away" team (swapped = true), and
    /// reading them positionally, as the NFL flow does, would flip the favorite and
    /// both sides' odds.
    /// </summary>
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
            // Called for every tracked game on every sync, so only touch the row when the
            // outcome actually changed (a corrected score or line) — not to bump ResolvedAt.
            game.GameResult.HomeAtsResult = homeAts;
            game.GameResult.AwayAtsResult = awayAts;
            game.GameResult.OuResult      = ouResult;
            game.GameResult.ResolvedAt    = DateTime.UtcNow;
        }
    }

    // -------------------------------------------------------------------------
    // Games and teams
    // -------------------------------------------------------------------------

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

        // Saved now so game.Id exists before the GameLine/GameResult that reference it.
        await _db.SaveChangesAsync(ct);
        return game;
    }

    /// <summary>
    /// Teams are created the first time a game with betting lines involves them, straight
    /// from the match payload — there's no bulk team seed (Highlightly's NCAA list is ~755
    /// teams, mostly ones that never get lines). This also means every stored CFB team
    /// has a betting-covered game, and abbreviations among them are unique even though
    /// they aren't across Highlightly's full list.
    /// </summary>
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
                NbaApiId     = externalId,   // reusing field as generic external ID
                Name         = teamRef.DisplayName,
                Abbreviation = teamRef.Abbreviation,
                UpdatedAt    = DateTime.UtcNow
            };
            _db.Teams.Add(team);
            await _db.SaveChangesAsync(ct);   // populate team.Id before a game references it
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
            _             => "POSTPONED"   // Postponed, Suspended, Cancelled, Abandoned, Unknown
        };

        var (homeScore, awayScore) = ParseScore(
            match.State.Score?.Current, match.Id, homeTeamName, awayTeamName);
        bool overtime = !string.IsNullOrEmpty(match.State.Score?.FirstOvertimePeriod);

        // Runs for every tracked game on every sync — only stamp UpdatedAt when
        // something actually changed, so an idle re-sync doesn't rewrite ~100 rows.
        bool changed = game.Status != status
            || game.HomeScore != homeScore
            || game.AwayScore != awayScore
            || (overtime && game.WentToOvertime != true)
            || game.GameDate != date;

        // A game rescheduled to another day since it was first stored gets its date corrected.
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

    /// <summary>Parses Highlightly's combined "H - A" score string.</summary>
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
