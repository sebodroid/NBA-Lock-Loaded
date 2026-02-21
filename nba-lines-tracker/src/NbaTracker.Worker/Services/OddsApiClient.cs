using System.Net.Http.Json;
using Microsoft.Extensions.Http.Resilience;
using NbaTracker.Worker.Models.OddsApi;

namespace NbaTracker.Worker.Services;

public class OddsApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<OddsApiClient> _logger;
    private readonly string _apiKey;
    private readonly string _primaryBookmaker;
    private readonly string _fallbackBookmaker;
    private bool _bookmakersLogged;

    public OddsApiClient(HttpClient http, IConfiguration config, ILogger<OddsApiClient> logger)
    {
        _http = http;
        _logger = logger;
        _apiKey = config["OddsApi:ApiKey"] ?? throw new InvalidOperationException("OddsApi:ApiKey configuration is required.");
        _primaryBookmaker = config["OddsApi:PrimaryBookmaker"] ?? "fanduel";
        _fallbackBookmaker = config["OddsApi:FallbackBookmaker"] ?? "hardrockbet";
    }

    /// <summary>
    /// Fetches pre-game spreads and totals for all NBA events in a single request.
    /// On the first call, logs all available bookmaker keys at Debug level so
    /// the HardRock key (OddsApi:FallbackBookmaker) can be validated.
    /// </summary>
    public async Task<List<OddsApiEvent>> GetOddsAsync(CancellationToken ct)
    {
        // Append bookmaker filter to limit response size (only the two we care about)
        var path = $"v4/sports/basketball_nba/odds?regions=us&markets=spreads,totals" +
                   $"&bookmakers={_primaryBookmaker},{_fallbackBookmaker}" +
                   $"&apiKey={_apiKey}";

        _logger.LogInformation("Fetching NBA odds from The Odds API (markets: spreads, totals)");

        var events = await _http.GetFromJsonAsync<List<OddsApiEvent>>(path, ct)
            ?? throw new InvalidOperationException("The Odds API returned null response for odds endpoint.");

        // Log all bookmaker keys on first call so HardRock key can be validated
        if (!_bookmakersLogged)
        {
            _bookmakersLogged = true;
            var allKeys = events
                .SelectMany(e => e.Bookmakers)
                .Select(b => b.Key)
                .Distinct()
                .OrderBy(k => k);
            _logger.LogDebug("Available bookmakers: {Keys}", string.Join(", ", allKeys));
        }

        return events;
    }

    /// <summary>
    /// Fetches historical pre-game spreads and totals for a past date.
    /// Queries odds as they were at noon ET (17:00 UTC) on the given date —
    /// early enough to capture pre-game lines for all tip-offs.
    /// Requires a paid Odds API plan (Starter or above).
    /// </summary>
    public async Task<List<OddsApiEvent>> GetHistoricalOddsAsync(DateOnly date, CancellationToken ct)
    {
        // Query snapshot at 17:00 UTC (noon ET) — pre-game for all tip-offs that day
        var snapshotTime = new DateTime(date.Year, date.Month, date.Day, 17, 0, 0, DateTimeKind.Utc);
        var iso = snapshotTime.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var path = $"v4/historical/sports/basketball_nba/odds?regions=us&markets=spreads,totals" +
                   $"&bookmakers={_primaryBookmaker},{_fallbackBookmaker}" +
                   $"&date={iso}" +
                   $"&apiKey={_apiKey}";

        _logger.LogInformation("Fetching historical NBA odds for {Date} (snapshot: {Snapshot})", date, iso);

        var response = await _http.GetFromJsonAsync<OddsApiHistoricalResponse>(path, ct)
            ?? throw new InvalidOperationException($"The Odds API returned null for historical odds on {date}.");

        _logger.LogInformation("Fetched {Count} historical events for {Date}", response.Data.Count, date);
        return response.Data;
    }

    /// <summary>
    /// Fetches completed game scores for recent games.
    /// daysFrom: 1 to 3 (The Odds API maximum is 3 days back).
    /// </summary>
    public async Task<List<OddsApiScore>> GetScoresAsync(int daysFrom, CancellationToken ct)
    {
        if (daysFrom < 1 || daysFrom > 3)
            throw new ArgumentOutOfRangeException(nameof(daysFrom), "The Odds API supports 1 to 3 days back for scores.");

        var path = $"v4/sports/basketball_nba/scores?daysFrom={daysFrom}&apiKey={_apiKey}";

        _logger.LogInformation("Fetching NBA scores from The Odds API (daysFrom: {DaysFrom})", daysFrom);

        return await _http.GetFromJsonAsync<List<OddsApiScore>>(path, ct)
            ?? throw new InvalidOperationException("The Odds API returned null response for scores endpoint.");
    }

    // ---------------------------------------------------------------------------
    // Static helper methods — pure logic, no HTTP, no DI
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Selects the canonical bookmaker for a given event, preferring the primary key
    /// and falling back to the secondary key if the primary is unavailable.
    /// </summary>
    public static OddsApiBookmaker? SelectCanonicalBookmaker(
        List<OddsApiBookmaker> bookmakers,
        string primaryKey,
        string fallbackKey)
        => bookmakers.FirstOrDefault(b => b.Key == primaryKey)
           ?? bookmakers.FirstOrDefault(b => b.Key == fallbackKey);

    /// <summary>
    /// Extracts spread information from a spreads market.
    /// The favorite is identified by the negative (lower) point value.
    /// Returns spread as absolute value with favorite team name and odds for both sides.
    /// </summary>
    public static (decimal Spread, string FavoriteTeamName, int FavoriteSpreadOdds, int UnderdogSpreadOdds)
        ExtractSpread(OddsApiMarket spreadsMarket)
    {
        var favorite = spreadsMarket.Outcomes.MinBy(o => o.Point ?? 0)!;
        var underdog = spreadsMarket.Outcomes.First(o => o.Name != favorite.Name);
        return (Math.Abs(favorite.Point!.Value), favorite.Name, (int)Math.Round(favorite.Price), (int)Math.Round(underdog.Price));
    }

    /// <summary>
    /// Extracts total line and odds from a totals market.
    /// Returns (total line, over odds, under odds).
    /// </summary>
    public static (decimal Total, int OverOdds, int UnderOdds) ExtractTotal(OddsApiMarket totalsMarket)
    {
        var over = totalsMarket.Outcomes.First(o => o.Name == "Over");
        var under = totalsMarket.Outcomes.First(o => o.Name == "Under");
        return (over.Point!.Value, (int)Math.Round(over.Price), (int)Math.Round(under.Price));
    }
}
