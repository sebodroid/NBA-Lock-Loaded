namespace NbaTracker.Api.Models;

public record CreateBetRequest(
    int GameId,
    string Kind,                // "PlayerProp" | "Spread" | "Total"
    int? PlayerPropLineId,      // required when Kind == "PlayerProp"
    string Side,                 // "Over" | "Under" (PlayerProp/Total) | "Home" | "Away" (Spread)
    decimal StakeAmount
);

public record BetResponse(
    int Id,
    string Kind,
    int GameId,
    string GameLabel,           // "SF @ LAR"
    string GameDate,
    string GameStatus,
    int? PlayerId,
    string? PlayerName,
    string? TeamAbbreviation,
    string MarketLabel,
    decimal LineAtBet,
    string Side,
    int OddsAtBet,
    decimal StakeAmount,
    decimal ToWinAmount,
    string Outcome,              // "Pending" | "Won" | "Lost" | "Push" — always computed live, never stored
    decimal? ActualValue,
    string PlacedAt              // ISO 8601
);
