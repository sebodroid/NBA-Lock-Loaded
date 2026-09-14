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
                     && s.Category == mapping.Category
                     && s.StatName.ToLower().Contains(keyword)
                     && s.Value.HasValue)
            .Select(s => new { s.StatName, s.Value })
            .ToListAsync(ct);

        var filtered = rows.Where(r => !mapping.Exclude.Any(x => r.StatName.ToLower().Contains(x))).ToList();
        return filtered.Count > 0 ? filtered.Max(r => r.Value!.Value) : null;
    }
}
