namespace NbaTracker.Api.Models;

// Side is "Over"/"Under" for totals, or "Team" for a spread/moneyline outcome. TeamId is
// null for game-level totals. Line is signed for spreads and null for moneylines.
public record TeamPropOutcomeResponse(
    string Side,
    int? TeamId,
    string? TeamAbbr,
    decimal? Line,
    int Price
);

// One market (e.g. "Team Total") as posted by a single bookmaker.
public record TeamPropMarketResponse(
    string MarketKey,
    string Label,
    string Group,           // "Full Game" / "1st Half" / "1st Quarter"
    string Bookmaker,
    string UpdatedAt,       // ISO 8601 UTC — when these lines were last fetched
    List<TeamPropOutcomeResponse> Outcomes
);
