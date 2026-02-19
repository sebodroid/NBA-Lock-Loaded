using System.Text.Json.Serialization;

namespace NbaTracker.Worker.Models.BallDontLie;

public class BdlPagedResponse<T>
{
    [JsonPropertyName("data")] public List<T> Data { get; set; } = [];
    [JsonPropertyName("meta")] public BdlMeta Meta { get; set; } = null!;
}

public class BdlMeta
{
    [JsonPropertyName("next_cursor")] public int? NextCursor { get; set; }
    [JsonPropertyName("per_page")] public int PerPage { get; set; }
}
