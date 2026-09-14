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
   public async Task<List<OddsApiEvent>> GetOddsAsync(CancellationToken ct, string sportKey = "basketball_nba")
{
    var path = $"v4/sports/{sportKey}/odds?regions=us&markets=spreads,totals,h2h" +
               $"&bookmakers={_primaryBookmaker},{_fallbackBookmaker}" +
               $"&oddsFormat=american" +
               $"&apiKey={_apiKey}";

    _logger.LogInformation("Fetching odds from The Odds API (sport: {Sport})", sportKey);

    var events = await _http.GetFromJsonAsync<List<OddsApiEvent>>(path, ct)
        ?? throw new InvalidOperationException("The Odds API returned null response for odds endpoint.");

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
   public async Task<List<OddsApiEvent>> GetHistoricalOddsAsync(DateOnly date, CancellationToken ct, string sportKey = "basketball_nba")
{
    var snapshotTime = new DateTime(date.Year, date.Month, date.Day, 17, 0, 0, DateTimeKind.Utc);
    var iso = snapshotTime.ToString("yyyy-MM-ddTHH:mm:ssZ");

    var path = $"v4/historical/sports/{sportKey}/odds?regions=us&markets=spreads,totals,h2h" +
               $"&bookmakers={_primaryBookmaker},{_fallbackBookmaker}" +
               $"&oddsFormat=american" +
               $"&date={iso}" +
               $"&apiKey={_apiKey}";

    _logger.LogInformation("Fetching historical odds for {Date} (sport: {Sport}, snapshot: {Snapshot})",
        date, sportKey, iso);

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

    /// <summary>
    /// Fetches player prop markets for a single event. Unlike spreads/totals, props are
    /// not available on the bulk /odds endpoint — this is a separate, per-event call,
    /// so it should only be made for games actually worth showing props for (today's
    /// games), not the full rolling window.
    /// </summary>
    public async Task<OddsApiEvent?> GetEventPropsAsync(
        string sportKey, string eventId, IEnumerable<string> markets, IEnumerable<string> bookmakers, CancellationToken ct)
    {
        var marketsParam = string.Join(",", markets);
        var bookmakersParam = string.Join(",", bookmakers);
        var path = $"v4/sports/{sportKey}/events/{eventId}/odds?regions=us&markets={marketsParam}" +
                   $"&bookmakers={bookmakersParam}" +
                   $"&oddsFormat=american" +
                   $"&apiKey={_apiKey}";

        _logger.LogDebug("Fetching player props for event {EventId} (sport: {Sport})", eventId, sportKey);

        var response = await _http.GetAsync(path, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogDebug("No props available for event {EventId} ({StatusCode})",
                eventId, response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<OddsApiEvent>(cancellationToken: ct);
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
        => SelectCanonicalBookmaker(bookmakers, [primaryKey, fallbackKey]);

    /// <summary>
    /// Selects the first bookmaker present, in priority order — for cases like player
    /// props where more than two candidate bookmakers are worth checking.
    /// </summary>
    public static OddsApiBookmaker? SelectCanonicalBookmaker(
        List<OddsApiBookmaker> bookmakers, IEnumerable<string> priorityKeys)
    {
        foreach (var key in priorityKeys)
        {
            var match = bookmakers.FirstOrDefault(b => b.Key == key);
            if (match is not null) return match;
        }
        return null;
    }

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
