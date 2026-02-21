using System.Text.Json.Serialization;

namespace NbaTracker.Worker.Models.OddsApi;

/// <summary>
/// Wrapper returned by GET /v4/historical/sports/{sport}/odds
/// The events array is identical in structure to the live odds endpoint.
/// </summary>
public class OddsApiHistoricalResponse
{
    [JsonPropertyName("timestamp")] public DateTimeOffset Timestamp { get; set; }
    [JsonPropertyName("data")] public List<OddsApiEvent> Data { get; set; } = [];
}
