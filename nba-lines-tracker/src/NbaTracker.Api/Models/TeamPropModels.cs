namespace NbaTracker.Api.Models;

public record TeamPropOutcomeResponse(
    string Side,
    int? TeamId,
    string? TeamAbbr,
    decimal? Line,
    int Price
);

public record TeamPropMarketResponse(
    string MarketKey,
    string Label,
    string Group,
    string Bookmaker,
    string UpdatedAt,
    List<TeamPropOutcomeResponse> Outcomes
);
