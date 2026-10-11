using System.Text.Json;
using NbaTracker.Worker.Models.OddsApi;

namespace NbaTracker.Worker.Tests;

public class OddsApiHistoricalResponseTests
{
    [Fact]
    public void NullTimestamp_FromASportWithNoCoverageYet_Deserializes()
    {
        const string json = """
            {"timestamp":null,"previous_timestamp":null,"next_timestamp":"2026-08-28T13:10:38Z","data":[]}
            """;

        var response = JsonSerializer.Deserialize<OddsApiHistoricalResponse>(json)!;

        Assert.Null(response.Timestamp);
        Assert.Empty(response.Data);
    }

    [Fact]
    public void NormalResponse_StillDeserializes()
    {
        const string json = """
            {"timestamp":"2026-09-12T16:55:36Z","data":[{"id":"e1","home_team":"A","away_team":"B","commence_time":"2026-09-12T19:00:00Z","bookmakers":[]}]}
            """;

        var response = JsonSerializer.Deserialize<OddsApiHistoricalResponse>(json)!;

        Assert.NotNull(response.Timestamp);
        Assert.Single(response.Data);
    }
}
