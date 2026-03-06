namespace NbaTracker.Api.Models;

public record TeamSeasonStats(
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

public record H2HGameEntry(
    int GameId,
    string GameDate,       // "YYYY-MM-DD"
    int HomeTeamId,
    int? HomeScore,
    int? AwayScore,
    decimal? SpreadLine,
    decimal? TotalLine,
    string? HomeAtsResult, // "Cover" / "Loss" / "Push" / null
    string? AwayAtsResult,
    string? OuResult       // "Over" / "Under" / "Push" / null
);

public record TodayMatchupResponse(
    int GameId,
    string Status,
    int HomeTeamId,
    string HomeTeamName,
    string HomeTeamAbbr,
    int AwayTeamId,
    string AwayTeamName,
    string AwayTeamAbbr,
    decimal? SpreadLine,
    int? FavoriteTeamId,
    decimal? TotalLine,
    string? Bookmaker,
    TeamSeasonStats HomeStats,
    TeamSeasonStats AwayStats,
    List<H2HGameEntry> HeadToHead
);
