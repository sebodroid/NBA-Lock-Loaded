using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NbaTracker.Data;
using NbaTracker.Data.Entities;
using NbaTracker.Worker.Models.NflStats;
using NbaTracker.Worker.Models.OddsApi;

namespace NbaTracker.Worker.Services;

public class NflSyncOrchestrator
{
    private readonly NbaTrackerDbContext _db;
    private readonly NflStatsClient _nflClient;
    private readonly OddsApiClient _oddsClient;
    private readonly IConfiguration _config;
    private readonly ILogger<NflSyncOrchestrator> _logger;

    private static readonly TimeZoneInfo EasternTime = GameMatchingService.GetEasternTimeZone();

    // Confirmed against real 2026 preseason results (Steelers 28-9 Packers, Ravens 24-7
    // Eagles) — Highlightly's "current" score string is home-first.
    private const bool ScoreCurrentIsHomeFirst = true;

    private static readonly string[] PropMarkets =
    [
        "player_pass_yds", "player_pass_tds", "player_pass_completions",
        "player_rush_yds", "player_rush_attempts",
        "player_reception_yds", "player_receptions",
        "player_sacks", "player_solo_tackles",
    ];

    // Broader than the primary/fallback pair used for spreads/totals — lower-profile
    // games (preseason especially) often only get props posted by a subset of books,
    // so checking more of them costs nothing extra (Odds API bills by markets actually
    // returned, not bookmakers requested) and meaningfully improves coverage.
    private static readonly string[] PropBookmakerPriority =
        ["fanduel", "hardrockbet", "draftkings", "betmgm", "williamhill_us"];

    public NflSyncOrchestrator(
        NbaTrackerDbContext db,
        NflStatsClient nflClient,
        OddsApiClient oddsClient,
        IConfiguration config,
        ILogger<NflSyncOrchestrator> logger)
    {
        _db = db;
        _nflClient = nflClient;
        _oddsClient = oddsClient;
        _config = config;
        _logger = logger;
    }

    public async Task RunDailySyncAsync(DateOnly date, CancellationToken ct)
    {
        _logger.LogInformation("[NFL] Starting daily sync for {Date}", date);

        var syncRun = new SyncRun
        {
            Sport = "NFL",
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
            // Step a: Seed NFL teams if none exist yet
            await SeedTeamsIfEmptyAsync(ct);

            // Step b: Fetch NFL matches for the date
            var matches = await _nflClient.GetGamesAsync(date, ct);
            _logger.LogInformation("[NFL] Fetched {Count} matches for {Date}", matches.Count, date);

            // Step c: Upsert each match, then capture player box score stats for FINAL games
            foreach (var match in matches)
            {
                try
                {
                    var game = await UpsertGameAsync(match, date, ct);
                    await _db.SaveChangesAsync(ct);
                    gamesProcessed++;

                    await SyncBoxScoreIfNeededAsync(match.Id, game, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[NFL] Failed to upsert match {MatchId}", match.Id);
                    errors.Add($"NFL match {match.Id}: {ex.Message}");
                }
            }

            // Step d: Match games to odds and upsert lines/results — but skip the Odds API
            // call entirely once every game for this date is already FINAL with both a
            // GameLine and a GameResult on record. Those never change again, yet the rolling
            // window re-visits the same past days on every sync (daily via the schedule, or
            // every manual run) — without this check, a spread that settled weeks ago would
            // still burn a paid Historical Odds API call forever. Games missing a line/result
            // (e.g. a late score correction) still get re-fetched.
            var dbGames = await _db.Games
                .Include(g => g.HomeTeam)
                .Include(g => g.AwayTeam)
                .Include(g => g.GameLine)
                .Include(g => g.GameResult)
                .Where(g => g.GameDate == date
                         && g.Sport == "NFL"
                         && g.Status != "POSTPONED")
                .ToListAsync(ct);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            bool needsOddsFetch = dbGames.Count > 0 && (date >= today || dbGames.Any(g =>
                g.Status != "FINAL" || g.GameLine is null || g.GameResult is null));

            // The Odds API splits preseason into its own sport key; regular-season odds
            // simply don't exist under "americanfootball_nfl" before kickoff.
            var sportKey = IsPreseasonDate(date)
                ? "americanfootball_nfl_preseason"
                : "americanfootball_nfl";

            if (!needsOddsFetch)
            {
                _logger.LogDebug("[NFL] Skipping odds fetch for {Date} — all games already settled", date);
            }
            else
            {
            var oddsEvents = date < today
                ? await _oddsClient.GetHistoricalOddsAsync(date, ct, sportKey)
                : await _oddsClient.GetOddsAsync(ct, sportKey);

            _logger.LogInformation("[NFL] Fetched {Count} events from Odds API", oddsEvents.Count);

            var oddsLookup = new Dictionary<string, OddsApiEvent>(StringComparer.Ordinal);
            foreach (var evt in oddsEvents)
            {
                try
                {
                    var key = NflGameMatchingService.BuildCanonicalKeyFromOddsApi(
                        evt.HomeTeam, evt.AwayTeam, evt.CommenceTime, EasternTime);
                    oddsLookup.TryAdd(key, evt);
                }
                catch (KeyNotFoundException)
                {
                    _logger.LogDebug("[NFL] Skipping unknown team: '{Home}' vs '{Away}'",
                        evt.HomeTeam, evt.AwayTeam);
                }
            }

            var primaryBookmaker  = _config["OddsApi:PrimaryBookmaker"]  ?? "fanduel";
            var fallbackBookmaker = _config["OddsApi:FallbackBookmaker"] ?? "hardrockbet";

            foreach (var game in dbGames)
            {
                try
                {
                    var key = NflGameMatchingService.BuildCanonicalKey(
                        game.HomeTeam.Abbreviation,
                        game.AwayTeam.Abbreviation,
                        date);

                    if (!oddsLookup.TryGetValue(key, out var oddsEvent))
                    {
                        _logger.LogDebug("[NFL] No odds match for game {GameId} ({Key})", game.Id, key);
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
                        _logger.LogDebug("[NFL] No canonical bookmaker for game {GameId}", game.Id);
                        await _db.SaveChangesAsync(ct);
                        continue;
                    }

                    var spreadsMarket = bookmaker.Markets.FirstOrDefault(m => m.Key == "spreads");
                    var totalsMarket  = bookmaker.Markets.FirstOrDefault(m => m.Key == "totals");

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

                        if (NflGameMatchingService.TeamNameToAbbreviation.TryGetValue(
                                favoriteTeamName, out var favoriteAbbr))
                        {
                            var favoriteTeam = await _db.Teams
                                .FirstOrDefaultAsync(t => t.Sport == "NFL" && t.Abbreviation == favoriteAbbr, ct);
                            favoriteTeamId = favoriteTeam?.Id;
                        }
                    }

                    if (totalsMarket is not null && totalsMarket.Outcomes.Count >= 2)
                    {
                        var (extractedTotal, extractedOverOdds, extractedUnderOdds)
                            = OddsApiClient.ExtractTotal(totalsMarket);
                        total     = extractedTotal;
                        overOdds  = extractedOverOdds;
                        underOdds = extractedUnderOdds;
                    }

                    if (game.GameLine is null)
                    {
                        var gameLine = new GameLine
                        {
                            GameId         = game.Id,
                            Spread         = spread,
                            FavoriteTeamId = favoriteTeamId,
                            Total          = total,
                            HomeSpreadOdds = homeSpreadOdds,
                            AwaySpreadOdds = awaySpreadOdds,
                            OverOdds       = overOdds,
                            UnderOdds      = underOdds,
                            Bookmaker      = bookmaker.Key,
                            LineTimestamp  = DateTime.UtcNow,
                            UpdatedAt      = DateTime.UtcNow
                        };
                        _db.GameLines.Add(gameLine);
                        game.GameLine = gameLine;
                    }
                    else
                    {
                        game.GameLine.Spread         = spread;
                        game.GameLine.FavoriteTeamId = favoriteTeamId;
                        game.GameLine.Total          = total;
                        game.GameLine.HomeSpreadOdds = homeSpreadOdds;
                        game.GameLine.AwaySpreadOdds = awaySpreadOdds;
                        game.GameLine.OverOdds       = overOdds;
                        game.GameLine.UnderOdds      = underOdds;
                        game.GameLine.Bookmaker      = bookmaker.Key;
                        game.GameLine.LineTimestamp  = DateTime.UtcNow;
                        game.GameLine.UpdatedAt      = DateTime.UtcNow;
                    }

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
                            _db.GameResults.Add(new GameResult
                            {
                                GameId        = game.Id,
                                HomeAtsResult = homeAts,
                                AwayAtsResult = awayAts,
                                OuResult      = ouResult,
                                ResolvedAt    = DateTime.UtcNow
                            });
                        }
                        else
                        {
                            game.GameResult.HomeAtsResult = homeAts;
                            game.GameResult.AwayAtsResult = awayAts;
                            game.GameResult.OuResult      = ouResult;
                            game.GameResult.ResolvedAt    = DateTime.UtcNow;
                        }
                    }

                    await _db.SaveChangesAsync(ct);
                    _logger.LogDebug("[NFL] Lines saved for game {GameId}", game.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[NFL] Failed to process lines for game {GameId}", game.Id);
                    errors.Add($"Lines for game {game.Id}: {ex.Message}");
                }
            }
            }

            // Step f: Player props — today's games only. Props are a separate per-event
            // call (not part of the bulk odds fetch above), so this is scoped tightly to
            // avoid burning API credits on the full rolling window.
            if (date == today)
            {
                foreach (var game in dbGames)
                {
                    try
                    {
                        await SyncPlayerPropsAsync(game, sportKey, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[NFL] Failed to sync player props for game {GameId}", game.Id);
                        errors.Add($"Props for game {game.Id}: {ex.Message}");
                    }
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
            _logger.LogError(ex, "[NFL] Fatal error during sync for {Date}", date);
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
                "[NFL] Sync for {Date}: {Status}, {Games} games, {Errors} errors",
                date, syncRun.Status, syncRun.GamesProcessed, errors.Count);
        }
    }

    /// <summary>
    /// Fetches and upserts player prop lines for one game. Returns silently (not an
    /// error) if props aren't available yet — normal for preseason and far-out games,
    /// since sportsbooks post player props close to kickoff.
    /// </summary>
    private async Task SyncPlayerPropsAsync(Game game, string sportKey, CancellationToken ct)
    {
        if (game.OddsApiGameId is null || game.Status == "POSTPONED") return;

        var propsEvent = await _oddsClient.GetEventPropsAsync(
            sportKey, game.OddsApiGameId, PropMarkets, PropBookmakerPriority, ct);
        if (propsEvent is null)
        {
            _logger.LogInformation("[NFL] No props response for game {GameId}", game.Id);
            return;
        }

        var bookmaker = OddsApiClient.SelectCanonicalBookmaker(propsEvent.Bookmakers, PropBookmakerPriority);
        if (bookmaker is null)
        {
            // Normal for lower-profile games (e.g. preseason finales) — sportsbooks
            // often don't post player props for them at all, not a pipeline failure.
            _logger.LogInformation(
                "[NFL] No player props posted for game {GameId} by any of [{Books}] ({BookmakerCount} bookmakers returned)",
                game.Id, string.Join(", ", PropBookmakerPriority), propsEvent.Bookmakers.Count);
            return;
        }

        _logger.LogInformation("[NFL] Props found for game {GameId}: {MarketCount} markets from {Bookmaker}",
            game.Id, bookmaker.Markets.Count, bookmaker.Key);

        int matchedCount = 0, unmatchedCount = 0;

        foreach (var market in bookmaker.Markets)
        {
            // Prop outcomes come as Over/Under pairs per player, keyed by Description
            var byPlayer = market.Outcomes
                .Where(o => !string.IsNullOrEmpty(o.Description))
                .GroupBy(o => o.Description!);

            foreach (var group in byPlayer)
            {
                var over  = group.FirstOrDefault(o => o.Name == "Over");
                var under = group.FirstOrDefault(o => o.Name == "Under");
                if (over?.Point is null) continue;

                // Odds API sometimes includes periods in initials ("A.J. Brown") where
                // Highlightly doesn't ("AJ Brown") — strip them before matching; periods
                // don't distinguish between different real people, so this is safe.
                var normalizedName = group.Key.Replace(".", "");

                var player = await _db.Players.FirstOrDefaultAsync(
                    p => p.Sport == "NFL" && p.Name.ToLower() == normalizedName.ToLower(), ct);

                player ??= await FindOrCreatePlayerByNameAsync(normalizedName, ct);

                if (player is null)
                {
                    unmatchedCount++;
                    _logger.LogInformation("[NFL] Prop line for unrecognized player: '{Name}' ({Market})",
                        group.Key, market.Key);
                    continue;
                }

                matchedCount++;

                var existing = await _db.PlayerPropLines.FirstOrDefaultAsync(
                    l => l.GameId == game.Id && l.PlayerId == player.Id && l.MarketKey == market.Key, ct);

                if (existing is null)
                {
                    _db.PlayerPropLines.Add(new PlayerPropLine
                    {
                        GameId        = game.Id,
                        PlayerId      = player.Id,
                        MarketKey     = market.Key,
                        Line          = over.Point.Value,
                        OverOdds      = (int)Math.Round(over.Price),
                        UnderOdds     = under is not null ? (int)Math.Round(under.Price) : null,
                        Bookmaker     = bookmaker.Key,
                        LineTimestamp = DateTime.UtcNow,
                        UpdatedAt     = DateTime.UtcNow
                    });
                }
                else
                {
                    existing.Line          = over.Point.Value;
                    existing.OverOdds      = (int)Math.Round(over.Price);
                    existing.UnderOdds     = under is not null ? (int)Math.Round(under.Price) : null;
                    existing.Bookmaker     = bookmaker.Key;
                    existing.LineTimestamp = DateTime.UtcNow;
                    existing.UpdatedAt     = DateTime.UtcNow;
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("[NFL] Props for game {GameId}: {Matched} matched, {Unmatched} unrecognized players",
            game.Id, matchedCount, unmatchedCount);
    }

    /// <summary>
    /// Fallback for a prop referencing a player with no Player row yet (their first
    /// tracked game, or a team whose games we haven't captured a box score for).
    /// Only accepts an EXACT case-insensitive full-name match among Highlightly's search
    /// results — real players can legitimately share a name (e.g. multiple "Josh Allen"s
    /// across the league), so a fuzzy/partial match here risks attaching a prop line to
    /// the wrong person. Skips (returns null) rather than guess.
    /// </summary>
    private async Task<Player?> FindOrCreatePlayerByNameAsync(string name, CancellationToken ct)
    {
        var results = await _nflClient.SearchPlayerByNameAsync(name, ct);
        var exact = results.Where(r => string.Equals(r.FullName, name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count != 1)
        {
            _logger.LogInformation(
                "[NFL] Player search for '{Name}' returned {Count} exact matches (raw results: [{Raw}]) — skipping",
                name, exact.Count, string.Join(", ", results.Select(r => $"'{r.FullName}'")));
            return null;
        }

        var externalId = exact[0].Id.ToString();

        // Race guard: another concurrent call in this same sync could have just created
        // this exact player (e.g. two different prop markets referencing them).
        var existing = await _db.Players.FirstOrDefaultAsync(
            p => p.Sport == "NFL" && p.ExternalId == externalId, ct);
        if (existing is not null) return existing;

        var player = new Player
        {
            Sport      = "NFL",
            ExternalId = externalId,
            Name       = exact[0].FullName,
            UpdatedAt  = DateTime.UtcNow
        };
        _db.Players.Add(player);
        await _db.SaveChangesAsync(ct);   // populate player.Id before it's referenced by a prop line

        _logger.LogInformation("[NFL] Created player record for '{Name}' via search (first prop appearance)",
            player.Name);
        return player;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Whether a date falls before that NFL season's own regular-season start —
    /// season-aware, not just compared against the current year's boundary, so this
    /// stays correct for historical backfills (e.g. every 2025-season date must resolve
    /// against 2025's Sept 4 opener, not 2026's Sept 9 one).
    /// </summary>
    private static bool IsPreseasonDate(DateOnly date)
    {
        int seasonYear = date.Month <= 2 ? date.Year - 1 : date.Year;
        var seasonStart = SportSeasonBoundaries.NflRegularSeasonStarts.TryGetValue(seasonYear, out var start)
            ? start
            : SportSeasonBoundaries.NflRegularSeasonStart;   // unlisted year — best available fallback
        return date < seasonStart;
    }

    private async Task SeedTeamsIfEmptyAsync(CancellationToken ct)
    {
        if (await _db.Teams.AnyAsync(t => t.Sport == "NFL", ct))
            return;

        _logger.LogInformation("[NFL] No NFL teams found — seeding");
        var nflTeams = await _nflClient.GetTeamsAsync(ct);

        foreach (var t in nflTeams)
        {
            if (string.IsNullOrWhiteSpace(t.Abbreviation)) continue;

            var existing = await _db.Teams.FirstOrDefaultAsync(
                x => x.Sport == "NFL" && x.NbaApiId == t.Id.ToString(), ct);

            if (existing is null)
            {
                _db.Teams.Add(new Team
                {
                    Sport        = "NFL",
                    NbaApiId     = t.Id.ToString(),   // reusing field as generic external ID
                    Name         = t.DisplayName,
                    Abbreviation = t.Abbreviation,
                    UpdatedAt    = DateTime.UtcNow
                });
            }
            else
            {
                existing.Name         = t.DisplayName;
                existing.Abbreviation = t.Abbreviation;
                existing.UpdatedAt    = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("[NFL] Seeded {Count} NFL teams", nflTeams.Count);
    }

    private async Task<Game> UpsertGameAsync(HighlightlyMatch match, DateOnly date, CancellationToken ct)
    {
        string status = match.State.Description switch
        {
            "Scheduled"    => "SCHEDULED",
            "In progress"  => "LIVE",
            "Finished"     => "FINAL",
            _              => "POSTPONED"   // Postponed, Suspended, Cancelled, Abandoned, Unknown
        };

        string season = match.Season.ToString();
        var gameIdStr = match.Id.ToString();

        var homeTeam = (await _db.Teams.FirstOrDefaultAsync(
            t => t.Sport == "NFL" && t.NbaApiId == match.HomeTeam.Id.ToString(), ct))
            ?? throw new InvalidOperationException(
                $"NFL home team not found: {match.HomeTeam.Id} ({match.HomeTeam.DisplayName})");

        var awayTeam = (await _db.Teams.FirstOrDefaultAsync(
            t => t.Sport == "NFL" && t.NbaApiId == match.AwayTeam.Id.ToString(), ct))
            ?? throw new InvalidOperationException(
                $"NFL away team not found: {match.AwayTeam.Id} ({match.AwayTeam.DisplayName})");

        var (homeScore, awayScore) = ParseScore(
            match.State.Score?.Current, match.Id, homeTeam.Name, awayTeam.Name);
        bool? wentToOvertime = string.IsNullOrEmpty(match.State.Score?.FirstOvertimePeriod) ? null : true;

        var existing = await _db.Games.FirstOrDefaultAsync(
            g => g.Sport == "NFL" && g.NbaGameId == gameIdStr, ct);

        if (existing is null)
        {
            var game = new Game
            {
                Sport          = "NFL",
                NbaGameId      = gameIdStr,
                GameDate       = date,
                HomeTeamId     = homeTeam.Id,
                AwayTeamId     = awayTeam.Id,
                Status         = status,
                Season         = season,
                HomeScore      = homeScore,
                AwayScore      = awayScore,
                WentToOvertime = wentToOvertime,
                CreatedAt      = DateTime.UtcNow,
                UpdatedAt      = DateTime.UtcNow
            };
            _db.Games.Add(game);
            return game;
        }
        else
        {
            // Self-heals GameDate for any game stored under the wrong calendar day by
            // the UTC-vs-Eastern bucketing bug fixed in NflStatsClient (see comment there)
            // — every re-sync within the rolling window corrects it going forward.
            if (existing.GameDate != date)
            {
                _logger.LogInformation("[NFL] Correcting GameDate for match {MatchId}: {Old} -> {New}",
                    match.Id, existing.GameDate, date);
                existing.GameDate = date;
            }

            existing.Status         = status;
            existing.HomeScore      = homeScore;
            existing.AwayScore      = awayScore;
            existing.WentToOvertime = wentToOvertime ?? existing.WentToOvertime;
            existing.UpdatedAt      = DateTime.UtcNow;
            return existing;
        }
    }

    /// <summary>
    /// Captures per-player box score stats for a FINAL game, once, the first time it's
    /// seen as FINAL. Box scores don't change after a game ends, so this intentionally
    /// does not re-fetch on subsequent rolling-window syncs — bounds Highlightly request
    /// volume to roughly one call per game, ever, rather than once per day it's in the window.
    /// </summary>
    private async Task SyncBoxScoreIfNeededAsync(long highlightlyMatchId, Game game, CancellationToken ct)
    {
        if (game.Status != "FINAL") return;
        if (await _db.PlayerGameStats.AnyAsync(s => s.GameId == game.Id, ct)) return;

        var boxScore = await _nflClient.GetBoxScoreAsync(highlightlyMatchId, ct);

        foreach (var teamWrapper in boxScore)
        {
            var boxTeam = teamWrapper.Team;
            var dbTeam = await _db.Teams.FirstOrDefaultAsync(
                t => t.Sport == "NFL" && t.NbaApiId == boxTeam.Id.ToString(), ct);

            if (dbTeam is null)
            {
                _logger.LogDebug("[NFL] Box score team not found: {TeamId} ({Name})", boxTeam.Id, boxTeam.Name);
                continue;
            }

            foreach (var playerBox in boxTeam.BoxScores)
            {
                var externalId = playerBox.Player.Id.ToString();
                var player = await _db.Players.FirstOrDefaultAsync(
                    p => p.Sport == "NFL" && p.ExternalId == externalId, ct);

                if (player is null)
                {
                    player = new Player
                    {
                        Sport      = "NFL",
                        ExternalId = externalId,
                        Name       = playerBox.Player.Name,
                        Jersey     = playerBox.Player.Jersey,
                        TeamId     = dbTeam.Id,
                        UpdatedAt  = DateTime.UtcNow
                    };
                    _db.Players.Add(player);
                    await _db.SaveChangesAsync(ct);   // populate player.Id before referencing it below
                }
                else
                {
                    player.Name      = playerBox.Player.Name;
                    player.Jersey    = playerBox.Player.Jersey;
                    player.TeamId    = dbTeam.Id;
                    player.UpdatedAt = DateTime.UtcNow;
                }

                // Highlightly sometimes repeats the same (group, name) entry verbatim within
                // one stats block — confirmed in real team-level match data ("Third Down
                // Efficiency" and "Thrown Interceptions" both appeared twice). Dedupe here,
                // or a repeated entry would double-count in every downstream sum (leaderboards,
                // prop hit-rates). Keep the larger value on the rare chance duplicates disagree.
                var dedupedStats = playerBox.Statistics
                    .GroupBy(s => (s.Group, s.Name))
                    .Select(g => g.OrderByDescending(s => ParseStatValue(s.Value).Value ?? decimal.MinValue).First());

                foreach (var stat in dedupedStats)
                {
                    var (value, rawValue) = ParseStatValue(stat.Value);
                    _db.PlayerGameStats.Add(new PlayerGameStat
                    {
                        PlayerId  = player.Id,
                        GameId    = game.Id,
                        TeamId    = dbTeam.Id,
                        Category  = stat.Group,
                        StatName  = stat.Name,
                        Value     = value,
                        RawValue  = rawValue,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("[NFL] Captured box score for game {GameId} (match {MatchId})",
            game.Id, highlightlyMatchId);
    }

    /// <summary>
    /// Highlightly's stat "value" can be numeric or string per their docs — always keep
    /// the raw representation, and additionally parse a decimal when the value is purely
    /// numeric (stats like "12/18" completions-over-attempts won't parse and stay null).
    /// </summary>
    private static (decimal? Value, string RawValue) ParseStatValue(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return (element.GetDecimal(), element.GetRawText());
            case JsonValueKind.String:
                var s = element.GetString() ?? "";
                return (decimal.TryParse(s, out var d) ? d : null, s);
            default:
                return (null, element.GetRawText());
        }
    }

    /// <summary>
    /// Parses Highlightly's combined "H - A" score string.
    /// UNCONFIRMED order — logs the raw string every time so it can be spot-checked
    /// against a known real final score. Flip ScoreCurrentIsHomeFirst if it's backwards.
    /// </summary>
    private (int? Home, int? Away) ParseScore(
        string? current, long matchId, string homeTeamName, string awayTeamName)
    {
        if (string.IsNullOrWhiteSpace(current)) return (null, null);

        var parts = current.Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !int.TryParse(parts[0], out var first)
            || !int.TryParse(parts[1], out var second))
        {
            _logger.LogWarning("[NFL] Unparseable score '{Score}' for match {MatchId}", current, matchId);
            return (null, null);
        }

        var (home, away) = ScoreCurrentIsHomeFirst ? (first, second) : (second, first);

        _logger.LogDebug(
            "[NFL] Score parsed — match {MatchId} raw='{Raw}' → {HomeTeam}={Home} {AwayTeam}={Away}",
            matchId, current, homeTeamName, home, awayTeamName, away);

        return (home, away);
    }
}
