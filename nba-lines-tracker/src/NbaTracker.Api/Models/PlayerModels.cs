namespace NbaTracker.Api.Models;

public record LeaderboardEntryResponse(
    int PlayerId,
    string Name,
    string? TeamAbbreviation,
    int GamesPlayed,
    Dictionary<string, decimal> Stats   // stat name (verbatim from data source) -> summed value this season
);
