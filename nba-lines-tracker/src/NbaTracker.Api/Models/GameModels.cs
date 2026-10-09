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

// Lightweight game listing for the manual-bet game picker — a bet might be for a game
// already played (logging a bet made elsewhere after the fact) or one still upcoming.
public record GameListEntry(
    int GameId,
    string GameDate,
    string Status,
    int HomeTeamId,
    string HomeTeamAbbr,
    string HomeTeamName,
    int AwayTeamId,
    string AwayTeamAbbr,
    string AwayTeamName
);

// One roster entry for the manual player-prop bet picker.
public record GamePlayerEntry(
    int PlayerId,
    string Name,
    string? TeamAbbreviation
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
    int? HomeSpreadOdds,
    int? AwaySpreadOdds,
    decimal? TotalLine,
    int? OverOdds,
    int? UnderOdds,
    string? Bookmaker,
    TeamSeasonStats HomeStats,
    TeamSeasonStats AwayStats,
    List<H2HGameEntry> HeadToHead
);
