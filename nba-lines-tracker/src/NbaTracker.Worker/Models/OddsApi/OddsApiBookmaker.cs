using System.Text.Json.Serialization;

namespace NbaTracker.Worker.Models.OddsApi;

public class OddsApiBookmaker
{
    [JsonPropertyName("key")] public string Key { get; set; } = null!;       // "fanduel", "hardrockbet"
    [JsonPropertyName("title")] public string Title { get; set; } = null!;
    [JsonPropertyName("markets")] public List<OddsApiMarket> Markets { get; set; } = [];
}

public class OddsApiMarket
{
    [JsonPropertyName("key")] public string Key { get; set; } = null!;       // "spreads", "totals"
    [JsonPropertyName("outcomes")] public List<OddsApiOutcome> Outcomes { get; set; } = [];
}

public class OddsApiOutcome
{
    [JsonPropertyName("name")] public string Name { get; set; } = null!;     // team name or "Over"/"Under"
    [JsonPropertyName("price")] public decimal Price { get; set; }           // American odds: -110 (API returns as float)
    [JsonPropertyName("point")] public decimal? Point { get; set; }          // spread: -7.5 fav; total: 220.5
}
