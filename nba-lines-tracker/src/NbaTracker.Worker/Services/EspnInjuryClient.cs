using System.Net.Http.Json;
using NbaTracker.Worker.Models.Espn;

namespace NbaTracker.Worker.Services;

// ESPN's public site API — no auth, no key, used here only to fill in injury status
// (Questionable/Doubtful/Out) next to a prop, which nothing else in this pipeline
// provides. Separate from Highlightly/Odds API entirely; a failure here should never
// break the rest of a sync, so callers wrap this in a try/catch and just skip the update.
public class EspnInjuryClient
{
    private readonly HttpClient _http;
    private readonly ILogger<EspnInjuryClient> _logger;

    public EspnInjuryClient(HttpClient http, ILogger<EspnInjuryClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<List<EspnInjuryEntry>> GetNflInjuriesAsync(CancellationToken ct)
    {
        var response = await _http.GetFromJsonAsync<EspnInjuriesResponse>(
            "apis/site/v2/sports/football/nfl/injuries", ct);

        var entries = response?.Injuries.SelectMany(t => t.Injuries).ToList() ?? [];
        _logger.LogInformation("Fetched {Count} injury report entries from ESPN", entries.Count);
        return entries;
    }
}
