namespace NbaTracker.Api.Models;

public record TeamStatsResponse(
    int TeamId,
    string Name,
    string Abbreviation,
    string? Conference,
    string? Division,
    int GamesPlayed,
    int Wins,
    int Losses,
    int AtsCovers,
    int AtsLosses,
    int AtsPushes,
    int OuOvers,
    int OuUnders,
    int OuPushes
);

public record HomeAwaySplit(
    int GamesPlayed,
    int Wins,
    int Losses,
    int AtsCovers,
    int AtsLosses,
    int AtsPushes,
    int OuOvers,
    int OuUnders,
    int OuPushes
);

public record TeamDetailResponse(
    int TeamId,
    string Name,
    string Abbreviation,
    string? Conference,
    string? Division,
    HomeAwaySplit Home,
    HomeAwaySplit Away
);

public record GameLogEntry(
    int GameId,
    DateOnly GameDate,
    string HomeTeamAbbr,
    string AwayTeamAbbr,
    int? HomeScore,
    int? AwayScore,
    bool IsHomeGame,
    decimal? SpreadLine,
    decimal? TotalLine,
    string? AtsResult,    // "Cover" / "Loss" / "Push" / null
    string? OuResult      // "Over" / "Under" / "Push" / null
);
