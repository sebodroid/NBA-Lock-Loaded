using System.Net.Http.Json;
using NbaTracker.Worker.Models.BallDontLie;

namespace NbaTracker.Worker.Services;

public class BallDontLieClient
{
    private readonly HttpClient _http;

    public BallDontLieClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Fetches all NBA games for a given season using cursor-based pagination.
    /// Optionally narrows to a single game date (ET date, e.g. 2025-11-01).
    /// </summary>
    public async Task<List<BdlGame>> GetGamesAsync(int season, DateOnly? date, CancellationToken ct)
    {
        var results = new List<BdlGame>();
        int? cursor = null;

        do
        {
            var url = $"games?seasons[]={season}&per_page=100";

            if (date.HasValue)
                url += $"&dates[]={date.Value:yyyy-MM-dd}";

            if (cursor.HasValue)
                url += $"&cursor={cursor.Value}";

            var page = await _http.GetFromJsonAsync<BdlPagedResponse<BdlGame>>(url, ct)
                ?? throw new InvalidOperationException($"BallDontLie API returned null response for: {url}");

            results.AddRange(page.Data);
            cursor = page.Meta?.NextCursor;

        } while (cursor.HasValue);

        return results;
    }

    /// <summary>
    /// Fetches all 30 NBA teams. Used to seed the Teams table on first run.
    /// </summary>
    public async Task<List<BdlTeam>> GetTeamsAsync(CancellationToken ct)
    {
        var results = new List<BdlTeam>();
        int? cursor = null;

        do
        {
            var url = "teams?per_page=100";

            if (cursor.HasValue)
                url += $"&cursor={cursor.Value}";

            var page = await _http.GetFromJsonAsync<BdlPagedResponse<BdlTeam>>(url, ct)
                ?? throw new InvalidOperationException($"BallDontLie API returned null response for: {url}");

            results.AddRange(page.Data);
            cursor = page.Meta?.NextCursor;

        } while (cursor.HasValue);

        return results;
    }
}
