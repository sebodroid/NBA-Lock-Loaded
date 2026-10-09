using System.Text.Json.Serialization;

namespace NbaTracker.Worker.Models.Espn;

// GET https://site.api.espn.com/apis/site/v2/sports/football/nfl/injuries — public,
// no API key required. Grouped by team; flattened to a single list by EspnInjuryClient.
public class EspnInjuriesResponse
{
    [JsonPropertyName("injuries")] public List<EspnTeamInjuries> Injuries { get; set; } = [];
}

public class EspnTeamInjuries
{
    [JsonPropertyName("displayName")] public string DisplayName { get; set; } = null!;
    [JsonPropertyName("injuries")] public List<EspnInjuryEntry> Injuries { get; set; } = [];
}

public class EspnInjuryEntry
{
    // Observed values: Active, Questionable, Doubtful, Out, Injured Reserve.
    // "Active" means they're not actually flagged — ESPN still lists everyone with a
    // recap blurb, healthy or not.
    [JsonPropertyName("status")] public string Status { get; set; } = null!;
    [JsonPropertyName("shortComment")] public string? ShortComment { get; set; }
    [JsonPropertyName("athlete")] public EspnAthlete Athlete { get; set; } = null!;
}

public class EspnAthlete
{
    [JsonPropertyName("displayName")] public string DisplayName { get; set; } = null!;
}
