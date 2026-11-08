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
    int OuPushes,
    decimal AvgPointsScored,
    decimal AvgPointsAllowed,

    // ATS record specifically in past games where this team was on the SAME side
    // (favorite/underdog) as they are in today's matchup, AND the spread was a similar
    // size (same magnitude bucket — see SpreadBucket in GameEndpoints). A team's blanket
    // season ATS% can hide a very different story at a specific spread size — a team
    // that covers well as a short favorite isn't necessarily the same team as a
    // double-digit favorite. Null when there's no today's-game spread to compare against,
    // or too few similar-sized games this season to say anything meaningful.
    int? SimilarSpreadAtsCovers,
    int? SimilarSpreadAtsLosses,
    string? SimilarSpreadLabel   // e.g. "favored by 7+" — describes the matched bucket
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
    List<H2HGameEntry> HeadToHead,
    GameWeatherSummary? Weather   // null for indoor/retractable-roof venues — nothing to flag
);

public record GameWeatherSummary(
    int? TemperatureF,
    string? WindSpeed,
    int? PrecipitationChancePct,
    string? ShortForecast
);
