using System.Text.Json.Serialization;

namespace NbaTracker.Worker.Models.Nws;

// GET /points/{lat},{lon} — resolves coordinates to the forecast endpoint for that
// grid cell. NWS's grid layout means this URL is stable for a given venue, but it's
// cheap enough (and simpler) to resolve it fresh each time rather than caching it.
public class NwsPointsResponse
{
    [JsonPropertyName("properties")] public NwsPointsProperties? Properties { get; set; }
}

public class NwsPointsProperties
{
    [JsonPropertyName("forecastHourly")] public string? ForecastHourly { get; set; }
}

// GET the forecastHourly URL from above — hour-by-hour out to ~7 days.
public class NwsForecastResponse
{
    [JsonPropertyName("properties")] public NwsForecastProperties? Properties { get; set; }
}

public class NwsForecastProperties
{
    [JsonPropertyName("periods")] public List<NwsForecastPeriod> Periods { get; set; } = [];
}

public class NwsForecastPeriod
{
    [JsonPropertyName("startTime")] public string StartTime { get; set; } = null!;
    [JsonPropertyName("temperature")] public int? Temperature { get; set; }
    [JsonPropertyName("windSpeed")] public string? WindSpeed { get; set; }        // e.g. "15 mph" — NWS gives this pre-formatted
    [JsonPropertyName("shortForecast")] public string? ShortForecast { get; set; } // e.g. "Chance Rain Showers"
    [JsonPropertyName("probabilityOfPrecipitation")] public NwsProbability? ProbabilityOfPrecipitation { get; set; }
}

public class NwsProbability
{
    [JsonPropertyName("value")] public int? Value { get; set; }
}
