using System.Text.Json.Serialization;

namespace NbaTracker.Worker.Models.BallDontLie;

public class BdlTeam
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("abbreviation")] public string Abbreviation { get; set; } = null!;
    [JsonPropertyName("full_name")] public string FullName { get; set; } = null!;
    [JsonPropertyName("name")] public string Name { get; set; } = null!;        // city name
    [JsonPropertyName("conference")] public string? Conference { get; set; }
    [JsonPropertyName("division")] public string? Division { get; set; }
}
