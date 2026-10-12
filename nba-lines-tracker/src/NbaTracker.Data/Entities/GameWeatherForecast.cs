namespace NbaTracker.Data.Entities;

// One row per outdoor-venue game within the forecast lookahead window — indoor/dome
// games never get a row at all (nothing to flag), and this gets refreshed as kickoff
// approaches since NWS's forecast naturally sharpens the closer it gets.
public class GameWeatherForecast
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public int? TemperatureF { get; set; }
    public string? WindSpeed { get; set; }           // NWS-formatted, e.g. "15 mph"
    public int? PrecipitationChancePct { get; set; }
    public string? ShortForecast { get; set; }       // e.g. "Chance Rain Showers"
    public DateTime UpdatedAt { get; set; }

    public Game Game { get; set; } = null!;
}
