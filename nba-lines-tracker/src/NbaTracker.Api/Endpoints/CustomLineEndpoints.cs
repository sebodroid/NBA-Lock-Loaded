using Microsoft.EntityFrameworkCore;
using NbaTracker.Data;
using NbaTracker.Data.Entities;

namespace NbaTracker.Api.Endpoints;

// "What if I bet this exact line?" — a player, a market, and a line the user types in
// themselves, not something a sportsbook has actually posted. Reuses the real estimate
// engine (the same math behind Hot Bets and Player Card) by building a synthetic,
// never-persisted PlayerPropLine around the user's inputs — the estimate honestly has
// no idea it isn't a real sportsbook line.
public static class CustomLineEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", GetCustomEstimateAsync);
    }

    // GET /api/{sport}/custom-line?playerId=1&marketKey=player_rush_yds&line=65.5
    private static async Task<IResult> GetCustomEstimateAsync(
        string sport, int playerId, string marketKey, decimal line,
        NbaTrackerDbContext db, CancellationToken ct)
    {
        if (!SportRoute.TryNormalize(sport, out sport))
            return Results.NotFound(new { error = $"Unknown sport: {sport}" });

        if (PropMarketMapping.TryGetMapping(marketKey) is null)
            return Results.BadRequest(new { error = "Unknown market." });

        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId && p.Sport == sport, ct);
        if (player is null) return Results.NotFound(new { error = "Player not found." });

        var today = ApiClock.Today;
        var statsStart = SeasonHelper.StatsStartDate(sport, today);

        // The player's own team's next scheduled game, purely for opponent-defense
        // context — this doesn't need to be a game with any real props posted, since
        // nothing here gets saved.
        var upcomingGame = player.TeamId is null ? null : await db.Games
            .Where(g => g.Sport == sport && g.GameDate >= today
                     && g.Status != "FINAL" && g.Status != "POSTPONED"
                     && (g.HomeTeamId == player.TeamId || g.AwayTeamId == player.TeamId))
            .OrderBy(g => g.GameDate)
            .FirstOrDefaultAsync(ct);

        // BuildPropEstimateAsync reads line.Game.HomeTeamId/AwayTeamId directly (no DB
        // round-trip), so it needs a Game object either way. Sentinel IDs (-1) when there's
        // no real upcoming game ensure they can never equal player.TeamId, which sends
        // BuildPropEstimateAsync down its existing "no opponent context" path — the same
        // one a real prop takes when the opponent genuinely can't be resolved.
        var gameForContext = upcomingGame ?? new Game { HomeTeamId = -1, AwayTeamId = -1 };

        var syntheticLine = new PlayerPropLine
        {
            PlayerId = player.Id,
            GameId = upcomingGame?.Id ?? 0,
            MarketKey = marketKey,
            Line = line,
            Bookmaker = "custom",
            Game = gameForContext,
        };

        var estimate = await PlayerEndpoints.BuildPropEstimateAsync(player, syntheticLine, statsStart, db, ct);
        return Results.Ok(estimate);
    }
}
