namespace NbaTracker.Api.Models;

public record PropEstimate(
    int PropLineId,
    string MarketKey,
    string MarketLabel,
    string Category,          // "Passing" | "Rushing" | "Receiving" | "Defense" | "Other"
    decimal Line,
    int? OverOdds,
    int? UnderOdds,
    string Bookmaker,          // Over side's best-priced book
    string? UnderBookmaker,    // Under side's best-priced book — can differ from Bookmaker
    string OpponentAbbreviation,

    // Which season the history numbers below are drawn from: "current" once the player
    // has enough games this season, otherwise "prior" (last completed season) so there's
    // something to show early in the year. Rendered as a "Based on 2025" note.
    string StatBasis,

    // Player's own history against this stat, for the StatBasis season
    int GamesWithData,
    decimal? SeasonAverage,
    int HitCount,              // games where actual value exceeded the line
    decimal? HitRatePct,       // HitCount / GamesWithData, null if GamesWithData == 0

    // Opponent defensive context — null if not enough league data yet
    decimal? OpponentAllowedAverage,
    decimal? LeagueAllowedAverage,

    // Player's hit rate against a line adjusted for this specific opponent's tendency
    // in this stat, relative to league average. Not a statistical probability — an
    // explainable heuristic: how often would he have cleared a bar shifted by the same
    // ratio his opponent's average allowed differs from the league average.
    decimal? EstimatedHitRatePct,

    // Recent-form window (last 5 games played, regardless of statBasis) — a season-long
    // average can't see a role change, an injury return, or a defense's adjustment, so
    // this is shown alongside it rather than replacing it.
    int RecentGamesWithData,
    decimal? RecentHitRatePct
);

public record PlayerCardResponse(
    int PlayerId,
    string Name,
    string? TeamAbbreviation,
    int GamesPlayed,
    Dictionary<string, decimal> SeasonStats,
    List<PropEstimate> UpcomingProps,
    string? InjuryStatus,   // "Questionable" | "Doubtful" | "Out" | "Injured Reserve" | null (not listed)
    string? InjuryNote
);

// One player's prop line within the context of a specific game — the direct
// "what can I bet on tonight" view, reached from the game card rather than requiring
// the player to already appear in a leaderboard (which they won't, before their first
// game of a new season).
public record GamePropEntry(
    int PlayerId,
    string PlayerName,
    string? TeamAbbreviation,
    PropEstimate Estimate,
    string? InjuryStatus,
    string? InjuryNote
);

// One prop line surfaced on the "Hot Bets" list — same shape as GamePropEntry plus the
// game context, since a hot-bets list spans every upcoming game, not just one.
public record HotBetEntry(
    int PlayerId,
    string PlayerName,
    string? TeamAbbreviation,
    int GameId,
    string GameLabel,
    string GameDate,
    PropEstimate Estimate,
    string? InjuryStatus,
    string? InjuryNote
);
