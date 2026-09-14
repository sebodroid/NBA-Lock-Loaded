using System.Text.Json;
using NbaTracker.Worker.Models.NflStats;

namespace NbaTracker.Worker.Services;

public class NflStatsClient
{
    private readonly HttpClient _http;
    private readonly ILogger<NflStatsClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public NflStatsClient(HttpClient http, ILogger<NflStatsClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// Fetches NFL matches for a single date, following the data/pagination envelope
    /// until totalCount is exhausted (paid tiers shouldn't hit this, but free-tier
    /// responses can be paginated per the "plan" block in the docs).
    /// </summary>
    public async Task<List<HighlightlyMatch>> GetGamesAsync(DateOnly date, CancellationToken ct)
    {
        var dateStr = date.ToString("yyyy-MM-dd");
        var matches = new List<HighlightlyMatch>();
        int offset = 0;
        const int limit = 100;

        while (true)
        {
            // timezone=America/New_York — without it Highlightly buckets matches by UTC
            // calendar date, and NFL night games (8pm/10pm ET) fall after midnight UTC,
            // landing on the *next* UTC day and going missing from a "today" query.
            var url = $"matches?date={dateStr}&league=NFL&timezone=America/New_York&limit={limit}&offset={offset}";
            _logger.LogDebug("Fetching NFL matches for {Date} (offset {Offset})", dateStr, offset);

            var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var page = JsonSerializer.Deserialize<HighlightlyMatchesResponse>(json, JsonOptions);

            if (page is null || page.Data.Count == 0) break;
            matches.AddRange(page.Data);

            var total = page.Pagination?.TotalCount ?? matches.Count;
            offset += page.Data.Count;
            if (offset >= total) break;
        }

        _logger.LogInformation("Highlightly returned {Count} NFL matches for {Date}", matches.Count, dateStr);
        return matches;
    }

    public async Task<List<HighlightlyTeam>> GetTeamsAsync(CancellationToken ct)
    {
        var url = "teams?league=NFL";
        _logger.LogDebug("Fetching NFL teams");

        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var teams = JsonSerializer.Deserialize<List<HighlightlyTeam>>(json, JsonOptions) ?? [];

        _logger.LogInformation("Highlightly returned {Count} NFL teams", teams.Count);
        return teams;
    }

    /// <summary>
    /// Looks up a player by full name — used to create a Player record proactively
    /// when a prop line references someone we haven't seen in a box score yet (their
    /// very first tracked game, or a team whose games we haven't captured before).
    /// Without this, a brand-new player can never match a prop on their first game,
    /// since Player rows are otherwise only created reactively from box scores.
    /// </summary>
    public async Task<List<HighlightlyPlayerSearchResult>> SearchPlayerByNameAsync(string name, CancellationToken ct)
    {
        var url = $"players?name={Uri.EscapeDataString(name)}&limit=5";
        _logger.LogDebug("Searching Highlightly for player '{Name}'", name);

        var response = await _http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return [];

        var json = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<HighlightlyPlayerSearchResponse>(json, JsonOptions);
        return result?.Data ?? [];
    }

    /// <summary>
    /// Fetches the per-player box score for one completed match — the only source of
    /// player-level stats, since Highlightly has no season-stats or roster bulk endpoint.
    /// </summary>
    public async Task<List<HighlightlyBoxScoreTeamWrapper>> GetBoxScoreAsync(long matchId, CancellationToken ct)
    {
        var url = $"box-score/{matchId}";
        _logger.LogDebug("Fetching NFL box score for match {MatchId}", matchId);

        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var boxScore = JsonSerializer.Deserialize<List<HighlightlyBoxScoreTeamWrapper>>(json, JsonOptions) ?? [];

        _logger.LogDebug("Highlightly returned box score for match {MatchId} ({TeamCount} teams)",
            matchId, boxScore.Count);
        return boxScore;
    }
}
