using Microsoft.EntityFrameworkCore;
using NbaTracker.Data;

namespace NbaTracker.Api;

// Resolves the actual value a player posted for one prop market in one specific game —
// used to grade a settled bet. Shares PropMarketMapping's category+keyword+exclude rules
// with PlayerEndpoints.BuildPropEstimateAsync (see there for why the exclude list exists),
// but scoped to a single game instead of a whole season.
public static class PropStatResolver
{
    public static async Task<decimal?> GetGameValueAsync(
        NbaTrackerDbContext db, int playerId, int gameId, string marketKey, CancellationToken ct)
    {
        var mapping = PropMarketMapping.TryGetMapping(marketKey);
        if (mapping is null) return null;

        var keyword = mapping.Keyword.ToLower();
        var rows = await db.PlayerGameStats
            .Where(s => s.PlayerId == playerId
                     && s.GameId == gameId
                     && mapping.StatCategories.Contains(s.Category)
                     && s.StatName.ToLower().Contains(keyword)
                     && s.Value.HasValue)
            .Select(s => new { s.StatName, s.Value })
            .ToListAsync(ct);

        var filtered = rows.Where(r => !mapping.Exclude.Any(x => r.StatName.ToLower().Contains(x))).ToList();
        if (filtered.Count > 0) return filtered.Max(r => r.Value!.Value);

        // No matching row for this player/category. That's ambiguous on its own — it
        // could mean the box score for this game hasn't been captured yet (genuinely
        // still pending), or it HAS been captured and this player simply recorded
        // nothing in this category — a confirmed zero, since Highlightly (like most
        // providers) omits a zero-stat category rather than writing an explicit 0 row.
        // Tell the two apart by whether the game has ANY box-score rows at all: if it
        // does, this player's absence from this category means zero, not unknown.
        var gameHasBoxScore = await db.PlayerGameStats.AnyAsync(s => s.GameId == gameId, ct);
        return gameHasBoxScore ? 0m : null;
    }
}
