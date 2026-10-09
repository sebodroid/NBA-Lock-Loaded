using Microsoft.EntityFrameworkCore;
using NbaTracker.Api.Models;
using NbaTracker.Data;
using NbaTracker.Data.Entities;

namespace NbaTracker.Api.Endpoints;

public static class BetEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", GetBetsAsync);
        group.MapPost("/", CreateBetAsync);
        group.MapDelete("/{id:int}", DeleteBetAsync);
    }

    // GET /api/{sport}/bets — every bet placed for this sport, newest first. Outcome is
    // never a stored status — it's graded live against the actual game result / box score
    // on every request, so it's always consistent with whatever the Worker has synced,
    // with no separate settlement job that could fall out of sync.
    private static async Task<IResult> GetBetsAsync(
        string sport, NbaTrackerDbContext db, CancellationToken ct)
    {
        if (!SportRoute.TryNormalize(sport, out sport))
            return Results.NotFound(new { error = $"Unknown sport: {sport}" });

        var bets = await db.Bets
            .Include(b => b.Game).ThenInclude(g => g.HomeTeam)
            .Include(b => b.Game).ThenInclude(g => g.AwayTeam)
            .Include(b => b.Game).ThenInclude(g => g.GameLine)
            .Include(b => b.Player)
            .Include(b => b.PlayerPropLine)
            .Where(b => b.Sport == sport)
            .OrderByDescending(b => b.PlacedAt)
            .ToListAsync(ct);

        var responses = new List<BetResponse>();
        foreach (var bet in bets)
            responses.Add(await BuildResponseAsync(bet, db, ct));

        return Results.Ok(responses);
    }

    // POST /api/{sport}/bets — places a bet, snapshotting the current line/odds onto the
    // bet itself so later line movement can never retroactively change what it's graded
    // against — the same guarantee a real sportsbook gives you at bet slip confirmation.
    private static async Task<IResult> CreateBetAsync(
        string sport, CreateBetRequest request, NbaTrackerDbContext db, CancellationToken ct)
    {
        if (!SportRoute.TryNormalize(sport, out sport))
            return Results.NotFound(new { error = $"Unknown sport: {sport}" });

        if (!Enum.TryParse<BetKind>(request.Kind, true, out var kind))
            return Results.BadRequest(new { error = $"Unknown bet kind: {request.Kind}" });

        if (!Enum.TryParse<BetSide>(request.Side, true, out var side))
            return Results.BadRequest(new { error = $"Unknown side: {request.Side}" });

        if (request.StakeAmount <= 0)
            return Results.BadRequest(new { error = "Stake must be greater than 0" });

        var game = await db.Games
            .Include(g => g.GameLine)
            .Include(g => g.HomeTeam)
            .Include(g => g.AwayTeam)
            .FirstOrDefaultAsync(g => g.Id == request.GameId && g.Sport == sport, ct);
        if (game is null) return Results.NotFound(new { error = "Game not found" });

        var bet = new Bet
        {
            Sport = sport,
            GameId = game.Id,
            Kind = kind,
            Side = side,
            StakeAmount = request.StakeAmount,
            PlacedAt = DateTime.UtcNow,
            MarketLabel = ""   // filled in per-kind below
        };

        bool manual = request.PlayerPropLineId is null
            && request.ManualLine is not null && request.ManualOdds is not null;
        bet.IsManual = manual;

        switch (kind)
        {
            case BetKind.PlayerProp:
            {
                if (side is not (BetSide.Over or BetSide.Under))
                    return Results.BadRequest(new { error = "side must be Over or Under for a PlayerProp bet" });

                if (request.PlayerPropLineId is not null)
                {
                    // Auto mode — a line this app actually synced.
                    var line = await db.PlayerPropLines
                        .FirstOrDefaultAsync(l => l.Id == request.PlayerPropLineId && l.GameId == game.Id, ct);
                    if (line is null) return Results.NotFound(new { error = "Prop line not found" });

                    var odds = side == BetSide.Over ? line.OverOdds : line.UnderOdds;
                    if (odds is null) return Results.BadRequest(new { error = $"No {side} odds posted for this line" });

                    bet.PlayerPropLineId = line.Id;
                    bet.PlayerId = line.PlayerId;
                    bet.MarketKey = line.MarketKey;
                    bet.MarketLabel = PropMarketMapping.TryGetMapping(line.MarketKey)?.Label
                        ?? PropMarketMapping.PrettifyKey(line.MarketKey);
                    bet.LineAtBet = line.Line;
                    bet.OddsAtBet = odds.Value;
                }
                else
                {
                    // Manual mode — placed on another book, logged here by hand.
                    if (request.PlayerId is null)
                        return Results.BadRequest(new { error = "playerId is required for a manual PlayerProp bet" });
                    if (string.IsNullOrWhiteSpace(request.MarketKey))
                        return Results.BadRequest(new { error = "marketKey is required for a manual PlayerProp bet" });
                    if (request.ManualLine is null || request.ManualOdds is null)
                        return Results.BadRequest(new { error = "line and odds are required for a manual bet" });

                    var player = await db.Players.FirstOrDefaultAsync(
                        p => p.Id == request.PlayerId && p.Sport == sport, ct);
                    if (player is null) return Results.NotFound(new { error = "Player not found" });

                    bet.PlayerId = player.Id;
                    bet.MarketKey = request.MarketKey;
                    bet.MarketLabel = request.MarketLabel
                        ?? PropMarketMapping.TryGetMapping(request.MarketKey)?.Label
                        ?? PropMarketMapping.PrettifyKey(request.MarketKey);
                    bet.LineAtBet = request.ManualLine.Value;
                    bet.OddsAtBet = request.ManualOdds.Value;
                }
                break;
            }
            case BetKind.Spread:
            {
                if (side is not (BetSide.Home or BetSide.Away))
                    return Results.BadRequest(new { error = "side must be Home or Away for a Spread bet" });

                bet.TeamAbbreviation = side == BetSide.Home ? game.HomeTeam.Abbreviation : game.AwayTeam.Abbreviation;
                bet.MarketLabel = "Spread";

                if (manual)
                {
                    // Manual mode — the line you actually got, signed the same way the
                    // auto path stores it: negative if you were giving points, positive
                    // if you were getting them, from your own side's perspective.
                    bet.LineAtBet = request.ManualLine!.Value;
                    bet.OddsAtBet = request.ManualOdds!.Value;
                }
                else
                {
                    if (game.GameLine?.Spread is null || game.GameLine.FavoriteTeamId is null)
                        return Results.BadRequest(new { error = "No spread posted for this game yet — enter your own line to log it manually" });

                    bool pickedIsFavorite = side == BetSide.Home
                        ? game.GameLine.FavoriteTeamId == game.HomeTeamId
                        : game.GameLine.FavoriteTeamId == game.AwayTeamId;

                    // Stored from the picked team's own perspective — negative when giving
                    // points (favorite), positive when getting points (underdog) — so
                    // grading can add it straight onto the picked team's scoring margin.
                    bet.LineAtBet = pickedIsFavorite ? -game.GameLine.Spread.Value : game.GameLine.Spread.Value;
                    bet.OddsAtBet = (side == BetSide.Home ? game.GameLine.HomeSpreadOdds : game.GameLine.AwaySpreadOdds)
                        ?? -110;
                }
                break;
            }
            case BetKind.Total:
            {
                if (side is not (BetSide.Over or BetSide.Under))
                    return Results.BadRequest(new { error = "side must be Over or Under for a Total bet" });

                bet.MarketLabel = "Total";

                if (manual)
                {
                    bet.LineAtBet = request.ManualLine!.Value;
                    bet.OddsAtBet = request.ManualOdds!.Value;
                }
                else
                {
                    if (game.GameLine?.Total is null)
                        return Results.BadRequest(new { error = "No total posted for this game yet — enter your own line to log it manually" });

                    bet.LineAtBet = game.GameLine.Total.Value;
                    bet.OddsAtBet = (side == BetSide.Over ? game.GameLine.OverOdds : game.GameLine.UnderOdds) ?? -110;
                }
                break;
            }
        }

        bet.ToWinAmount = ComputeToWin(bet.StakeAmount, bet.OddsAtBet);

        db.Bets.Add(bet);
        await db.SaveChangesAsync(ct);

        var reloaded = await db.Bets
            .Include(b => b.Game).ThenInclude(g => g.HomeTeam)
            .Include(b => b.Game).ThenInclude(g => g.AwayTeam)
            .Include(b => b.Game).ThenInclude(g => g.GameLine)
            .Include(b => b.Player)
            .Include(b => b.PlayerPropLine)
            .FirstAsync(b => b.Id == bet.Id, ct);

        return Results.Created($"/api/{sport}/bets/{bet.Id}", await BuildResponseAsync(reloaded, db, ct));
    }

    // DELETE /api/{sport}/bets/{id} — for a mis-entered bet. No edit endpoint: odds/lines
    // are point-in-time snapshots, not something that should be revised after the fact.
    private static async Task<IResult> DeleteBetAsync(
        string sport, int id, NbaTrackerDbContext db, CancellationToken ct)
    {
        if (!SportRoute.TryNormalize(sport, out sport))
            return Results.NotFound(new { error = $"Unknown sport: {sport}" });

        var bet = await db.Bets.FirstOrDefaultAsync(b => b.Id == id && b.Sport == sport, ct);
        if (bet is null) return Results.NotFound();

        db.Bets.Remove(bet);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<BetResponse> BuildResponseAsync(Bet bet, NbaTrackerDbContext db, CancellationToken ct)
    {
        // Self-heal bets placed before the MarketKey column existed: they still have a
        // valid PlayerPropLineId, just an empty MarketKey (the migration that added the
        // column couldn't backfill data it doesn't know how to derive on its own).
        // Backfilling it here — once, on read — means grading and CLV never need to care
        // which era a bet came from.
        if (bet.Kind == BetKind.PlayerProp && bet.MarketKey is null && bet.PlayerPropLine is not null)
        {
            bet.MarketKey = bet.PlayerPropLine.MarketKey;
            await db.SaveChangesAsync(ct);
        }

        var game = bet.Game;
        var (outcome, actualValue) = await GradeAsync(bet, db, ct);
        var (closingLine, closingOdds, clvPct) = await ComputeClvAsync(bet, db, ct);

        return new BetResponse(
            bet.Id,
            bet.Kind.ToString(),
            bet.IsManual,
            game.Id,
            $"{game.AwayTeam.Abbreviation} @ {game.HomeTeam.Abbreviation}",
            game.GameDate.ToString("yyyy-MM-dd"),
            game.Status,
            bet.PlayerId,
            bet.Player?.Name,
            bet.TeamAbbreviation,
            bet.MarketLabel,
            bet.LineAtBet,
            bet.Side.ToString(),
            bet.OddsAtBet,
            bet.StakeAmount,
            bet.ToWinAmount,
            outcome,
            actualValue,
            bet.PlacedAt.ToString("O"),
            closingLine,
            closingOdds,
            clvPct
        );
    }

    // Closing-line value: compares the bet's own snapshotted line/odds to the same
    // market's current value, read once the game has actually started (lines stop
    // moving once kickoff happens — nothing re-fetches a live/final game's pregame
    // markets — so "current value post-kickoff" doubles as "closing value" without
    // needing a separate snapshot-at-kickoff job).
    //
    // ClvPct is odds-based, not line-based: it's the gap between the implied probability
    // of the bet's odds and the closing odds at the SAME side. Positive means the market
    // now considers your side more likely than your price implied when you bet it — the
    // standard definition of "beating the close." It does not attempt to re-price a line
    // that moved to a different number entirely; ClosingLine is shown alongside as
    // context for that.
    private static async Task<(decimal? ClosingLine, int? ClosingOdds, decimal? ClvPct)> ComputeClvAsync(
        Bet bet, NbaTrackerDbContext db, CancellationToken ct)
    {
        var game = bet.Game;
        if (game.Status == "SCHEDULED") return (null, null, null);   // not closed yet

        decimal? closingLine = null;
        int? closingOdds = null;

        switch (bet.Kind)
        {
            case BetKind.Total:
                closingLine = game.GameLine?.Total;
                closingOdds = bet.Side == BetSide.Over ? game.GameLine?.OverOdds : game.GameLine?.UnderOdds;
                break;

            case BetKind.Spread:
                if (game.GameLine?.Spread is not null && game.GameLine.FavoriteTeamId is not null)
                {
                    bool pickedIsFavorite = bet.Side == BetSide.Home
                        ? game.GameLine.FavoriteTeamId == game.HomeTeamId
                        : game.GameLine.FavoriteTeamId == game.AwayTeamId;
                    closingLine = pickedIsFavorite ? -game.GameLine.Spread.Value : game.GameLine.Spread.Value;
                    closingOdds = bet.Side == BetSide.Home ? game.GameLine.HomeSpreadOdds : game.GameLine.AwaySpreadOdds;
                }
                break;

            case BetKind.PlayerProp:
                // Auto-tracked bets already have the line loaded; a manual bet has no
                // PlayerPropLine of its own, but this app may have synced that same
                // game/player/market anyway — cross-reference it for CLV even though the
                // bet itself was placed elsewhere.
                var propLine = bet.PlayerPropLine ?? (bet.PlayerId is not null && bet.MarketKey is not null
                    ? await db.PlayerPropLines.FirstOrDefaultAsync(
                        l => l.GameId == game.Id && l.PlayerId == bet.PlayerId && l.MarketKey == bet.MarketKey, ct)
                    : null);
                closingLine = propLine?.Line;
                closingOdds = bet.Side == BetSide.Over ? propLine?.OverOdds : propLine?.UnderOdds;
                break;
        }

        decimal? clvPct = closingOdds.HasValue
            ? Math.Round((ImpliedProbability(closingOdds.Value) - ImpliedProbability(bet.OddsAtBet)) * 100, 1)
            : null;

        return (closingLine, closingOdds, clvPct);
    }

    private static decimal ImpliedProbability(int americanOdds)
    {
        decimal o = americanOdds;
        return o > 0 ? 100m / (o + 100m) : -o / (-o + 100m);
    }

    // Grades a bet against live data — never a stored status. Spread/Total replay the
    // bet's own snapshotted line against the final score (not GameResult, which reflects
    // whatever the CLOSING line ended up being — a line that moved after this bet was
    // placed must never retroactively change how it's graded). PlayerProp reads the
    // actual box-score value via PropStatResolver.
    private static async Task<(string Outcome, decimal? ActualValue)> GradeAsync(
        Bet bet, NbaTrackerDbContext db, CancellationToken ct)
    {
        var game = bet.Game;
        if (game.Status != "FINAL" || game.HomeScore is null || game.AwayScore is null)
            return ("Pending", null);

        switch (bet.Kind)
        {
            case BetKind.Total:
            {
                decimal combined = game.HomeScore.Value + game.AwayScore.Value;
                if (combined == bet.LineAtBet) return ("Push", combined);
                bool over = combined > bet.LineAtBet;
                bool won = bet.Side == BetSide.Over ? over : !over;
                return (won ? "Won" : "Lost", combined);
            }
            case BetKind.Spread:
            {
                bool pickedHome = bet.Side == BetSide.Home;
                int pickedScore = pickedHome ? game.HomeScore.Value : game.AwayScore.Value;
                int oppScore    = pickedHome ? game.AwayScore.Value : game.HomeScore.Value;
                decimal margin  = pickedScore - oppScore + bet.LineAtBet;
                if (margin == 0) return ("Push", margin);
                return (margin > 0 ? "Won" : "Lost", margin);
            }
            case BetKind.PlayerProp:
            {
                if (bet.PlayerId is null || bet.MarketKey is null) return ("Pending", null);
                var actual = await PropStatResolver.GetGameValueAsync(
                    db, bet.PlayerId.Value, game.Id, bet.MarketKey, ct);
                if (actual is null) return ("Pending", null);
                if (actual == bet.LineAtBet) return ("Push", actual);
                bool over = actual > bet.LineAtBet;
                bool won = bet.Side == BetSide.Over ? over : !over;
                return (won ? "Won" : "Lost", actual);
            }
            default:
                return ("Pending", null);
        }
    }

    private static decimal ComputeToWin(decimal stake, int americanOdds) =>
        americanOdds > 0
            ? Math.Round(stake * americanOdds / 100m, 2)
            : Math.Round(stake * 100m / Math.Abs(americanOdds), 2);
}
