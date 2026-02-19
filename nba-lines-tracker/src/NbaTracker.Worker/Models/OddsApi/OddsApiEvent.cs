using System.Text.Json.Serialization;

namespace NbaTracker.Worker.Models.OddsApi;

public class OddsApiEvent
{
    [JsonPropertyName("id")] public string Id { get; set; } = null!;                    // UUID
    [JsonPropertyName("sport_key")] public string SportKey { get; set; } = null!;
    [JsonPropertyName("commence_time")] public DateTimeOffset CommenceTime { get; set; } // UTC
    [JsonPropertyName("home_team")] public string HomeTeam { get; set; } = null!;       // "Boston Celtics"
    [JsonPropertyName("away_team")] public string AwayTeam { get; set; } = null!;
    [JsonPropertyName("bookmakers")] public List<OddsApiBookmaker> Bookmakers { get; set; } = [];
}
