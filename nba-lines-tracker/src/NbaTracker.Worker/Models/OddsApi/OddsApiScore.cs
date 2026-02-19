using System.Text.Json.Serialization;

namespace NbaTracker.Worker.Models.OddsApi;

public class OddsApiScore
{
    [JsonPropertyName("id")] public string Id { get; set; } = null!;
    [JsonPropertyName("sport_key")] public string SportKey { get; set; } = null!;
    [JsonPropertyName("commence_time")] public DateTimeOffset CommenceTime { get; set; }
    [JsonPropertyName("completed")] public bool Completed { get; set; }
    [JsonPropertyName("home_team")] public string HomeTeam { get; set; } = null!;
    [JsonPropertyName("away_team")] public string AwayTeam { get; set; } = null!;
    [JsonPropertyName("scores")] public List<OddsApiTeamScore>? Scores { get; set; }
}

public class OddsApiTeamScore
{
    [JsonPropertyName("name")] public string Name { get; set; } = null!;
    [JsonPropertyName("score")] public string Score { get; set; } = null!;  // returned as string
}
