using System.Text.Json;
using NbaTracker.Worker.Models.MlbStats;

namespace NbaTracker.Worker.Services;

public class MlbStatsClient
{
    private readonly HttpClient _http;
    private readonly ILogger<MlbStatsClient> _logger;

    private const string BaseUrl = "https://statsapi.mlb.com/api/v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MlbStatsClient(HttpClient http, ILogger<MlbStatsClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<List<MlbGame>> GetGamesAsync(DateOnly date, CancellationToken ct)
    {
        var dateStr = date.ToString("yyyy-MM-dd");
        var url = $"{BaseUrl}/schedule?sportId=1&date={dateStr}&hydrate=linescore,team";

        _logger.LogDebug("Fetching MLB schedule for {Date}", dateStr);

        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var schedule = JsonSerializer.Deserialize<MlbScheduleResponse>(json, JsonOptions);

        var games = schedule?.Dates.FirstOrDefault()?.Games ?? [];
        _logger.LogInformation("MLB Stats API returned {Count} games for {Date}", games.Count, dateStr);
        return games;
    }

    public async Task<List<MlbTeamDetail>> GetTeamsAsync(CancellationToken ct)
    {
        var url = $"{BaseUrl}/teams?sportId=1&activeStatus=Yes";

        _logger.LogDebug("Fetching MLB teams");

        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var teamsResponse = JsonSerializer.Deserialize<MlbTeamsResponse>(json, JsonOptions);

        var teams = teamsResponse?.Teams ?? [];
        _logger.LogInformation("MLB Stats API returned {Count} teams", teams.Count);
        return teams;
    }
}