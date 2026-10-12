using System.Net.Http.Json;
using NbaTracker.Worker.Models.Nws;

namespace NbaTracker.Worker.Services;

public record WeatherForecast(int? TemperatureF, string? WindSpeed, int? PrecipitationChancePct, string? ShortForecast);

// National Weather Service — api.weather.gov. No key, no signup, free: Program.cs just
// sets a descriptive User-Agent, which is NWS's only requirement (an identifying
// contact, per their usage policy — not an auth credential). US-only, which is exactly
// what NFL needs, and its ~7-day forecast window matches the existing prop/odds
// lookahead everywhere else in this pipeline.
public class WeatherClient
{
    private readonly HttpClient _http;
    private readonly ILogger<WeatherClient> _logger;

    public WeatherClient(HttpClient http, ILogger<WeatherClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// Worst-case conditions (highest precipitation chance) across the plausible NFL
    /// kickoff window — noon to 11pm local time — on the given game date. We don't
    /// store an exact kickoff time anywhere in this app, so this is deliberately a
    /// conservative "could this game plausibly be affected" read rather than a precise
    /// point forecast for one specific hour. Returns null if the date is outside NWS's
    /// forecast range (too far out) or the lookup fails for any reason — weather is a
    /// nice-to-have signal, never something that should break a sync.
    /// </summary>
    public async Task<WeatherForecast?> GetGameDayForecastAsync(
        decimal latitude, decimal longitude, DateOnly gameDate, CancellationToken ct)
    {
        try
        {
            var pointsUrl = $"points/{latitude:0.####},{longitude:0.####}";
            var points = await _http.GetFromJsonAsync<NwsPointsResponse>(pointsUrl, ct);
            var hourlyUrl = points?.Properties?.ForecastHourly;
            if (hourlyUrl is null) return null;

            // Absolute URL from the API response, not relative to the configured base address.
            var hourly = await _http.GetFromJsonAsync<NwsForecastResponse>(hourlyUrl, ct);
            var periods = hourly?.Properties?.Periods ?? [];

            var windowPeriods = periods
                .Select(p => (Period: p, Parsed: DateTimeOffset.TryParse(p.StartTime, out var t) ? t : (DateTimeOffset?)null))
                .Where(x => x.Parsed is not null
                         && DateOnly.FromDateTime(x.Parsed.Value.LocalDateTime) == gameDate
                         && x.Parsed.Value.LocalDateTime.Hour is >= 12 and <= 23)
                .Select(x => x.Period)
                .ToList();

            if (windowPeriods.Count == 0) return null;

            var worst = windowPeriods.OrderByDescending(p => p.ProbabilityOfPrecipitation?.Value ?? 0).First();
            return new WeatherForecast(
                worst.Temperature, worst.WindSpeed, worst.ProbabilityOfPrecipitation?.Value, worst.ShortForecast);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Weather forecast lookup failed for ({Lat}, {Lon}) on {Date}",
                latitude, longitude, gameDate);
            return null;
        }
    }
}
