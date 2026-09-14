using System.Text.Json;
using System.Text.Json.Serialization;

namespace NbaTracker.Worker.Models.NflStats;

// GET /matches — wrapped in a data/pagination/plan envelope
public class HighlightlyMatchesResponse
{
    [JsonPropertyName("data")] public List<HighlightlyMatch> Data { get; set; } = [];
    [JsonPropertyName("pagination")] public HighlightlyPagination? Pagination { get; set; }
}

public class HighlightlyPagination
{
    [JsonPropertyName("totalCount")] public int TotalCount { get; set; }
    [JsonPropertyName("offset")] public int Offset { get; set; }
    [JsonPropertyName("limit")] public int Limit { get; set; }
}

public class HighlightlyMatch
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("date")] public string Date { get; set; } = null!;      // ISO 8601
    [JsonPropertyName("league")] public string League { get; set; } = null!;
    [JsonPropertyName("season")] public int Season { get; set; }
    [JsonPropertyName("homeTeam")] public HighlightlyTeamRef HomeTeam { get; set; } = null!;
    [JsonPropertyName("awayTeam")] public HighlightlyTeamRef AwayTeam { get; set; } = null!;
    [JsonPropertyName("state")] public HighlightlyState State { get; set; } = null!;
}

public class HighlightlyTeamRef
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = null!;
    [JsonPropertyName("displayName")] public string DisplayName { get; set; } = null!;
    [JsonPropertyName("abbreviation")] public string Abbreviation { get; set; } = null!;
}

public class HighlightlyState
{
    // Observed values: Scheduled, In progress, Finished, Postponed, Suspended, Cancelled, Abandoned, Unknown
    [JsonPropertyName("description")] public string Description { get; set; } = null!;
    [JsonPropertyName("score")] public HighlightlyScore? Score { get; set; }
}

public class HighlightlyScore
{
    // Combined "H - A" or "A - H" string — order is UNCONFIRMED, see NflSyncOrchestrator.ParseScore.
    [JsonPropertyName("current")] public string? Current { get; set; }
    [JsonPropertyName("firstOvertimePeriod")] public string? FirstOvertimePeriod { get; set; }
}

// GET /teams — bare array, no envelope
public class HighlightlyTeam
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = null!;
    [JsonPropertyName("displayName")] public string DisplayName { get; set; } = null!;
    [JsonPropertyName("abbreviation")] public string Abbreviation { get; set; } = null!;
    [JsonPropertyName("league")] public string? League { get; set; }
}

// GET /box-score/{matchId} — bare array of exactly two elements: [homeTeam, awayTeam]
public class HighlightlyBoxScoreTeamWrapper
{
    [JsonPropertyName("team")] public HighlightlyBoxScoreTeam Team { get; set; } = null!;
}

public class HighlightlyBoxScoreTeam
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = null!;
    [JsonPropertyName("boxScores")] public List<HighlightlyPlayerBoxScore> BoxScores { get; set; } = [];
}

public class HighlightlyPlayerBoxScore
{
    [JsonPropertyName("player")] public HighlightlyPlayerRef Player { get; set; } = null!;
    [JsonPropertyName("statistics")] public List<HighlightlyStatEntry> Statistics { get; set; } = [];
}

public class HighlightlyPlayerRef
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = null!;
    [JsonPropertyName("jersey")] public int? Jersey { get; set; }
}

public class HighlightlyStatEntry
{
    [JsonPropertyName("group")] public string Group { get; set; } = null!;
    [JsonPropertyName("name")] public string Name { get; set; } = null!;
    // Docs note this can be numeric or string — deserialize raw and interpret in the orchestrator.
    [JsonPropertyName("value")] public JsonElement Value { get; set; }
}

// GET /players?name=X — wrapped in a data/pagination envelope, no "plan" block observed here
public class HighlightlyPlayerSearchResponse
{
    [JsonPropertyName("data")] public List<HighlightlyPlayerSearchResult> Data { get; set; } = [];
}

public class HighlightlyPlayerSearchResult
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("fullName")] public string FullName { get; set; } = null!;
}
