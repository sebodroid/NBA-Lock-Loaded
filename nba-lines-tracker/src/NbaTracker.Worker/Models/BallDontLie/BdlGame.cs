using System.Text.Json.Serialization;

namespace NbaTracker.Worker.Models.BallDontLie;

public class BdlGame
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("date")] public string Date { get; set; } = null!;        // "2025-11-01" ET date
    [JsonPropertyName("season")] public int Season { get; set; }                // 2025 for 2025-26
    [JsonPropertyName("status")] public string Status { get; set; } = null!;   // "Final", "Q4", "7:30 pm ET"
    [JsonPropertyName("period")] public int Period { get; set; }                // 0=not started, 4=reg, 5+=OT
    [JsonPropertyName("postseason")] public bool Postseason { get; set; }
    [JsonPropertyName("postponed")] public bool Postponed { get; set; }
    [JsonPropertyName("home_team_score")] public int? HomeTeamScore { get; set; }
    [JsonPropertyName("visitor_team_score")] public int? VisitorTeamScore { get; set; }
    [JsonPropertyName("home_team")] public BdlTeam HomeTeam { get; set; } = null!;
    [JsonPropertyName("visitor_team")] public BdlTeam VisitorTeam { get; set; } = null!;
}
