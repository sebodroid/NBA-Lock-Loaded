using Microsoft.EntityFrameworkCore;
using NbaTracker.Data;

namespace NbaTracker.Api;

// Small shared helper so every endpoint that lists players (game props, hot bets, the
// player card) can attach injury status the same way, in one query per request rather
// than one query per player.
public static class InjuryLookup
{
    public static async Task<Dictionary<int, (string Status, string? Note)>> GetForPlayersAsync(
        NbaTrackerDbContext db, IEnumerable<int> playerIds, CancellationToken ct)
    {
        var ids = playerIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        var rows = await db.PlayerInjuryStatuses
            .Where(i => ids.Contains(i.PlayerId))
            .ToListAsync(ct);

        return rows.ToDictionary(i => i.PlayerId, i => (i.Status, i.Note));
    }
}
