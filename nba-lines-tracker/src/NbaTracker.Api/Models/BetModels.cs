namespace NbaTracker.Api.Models;

public record CreateBetRequest(
    int GameId,
    string Kind,                 // "PlayerProp" | "Spread" | "Total"
    string Side,                  // "Over" | "Under" (PlayerProp/Total) | "Home" | "Away" (Spread)
    decimal StakeAmount,

    // Auto mode (bet placed and tracked through this app): reference a line this app
    // actually synced. PlayerPropLineId for PlayerProp; Spread/Total read the game's
    // current GameLine instead.
    int? PlayerPropLineId,

    // Manual mode (bet placed elsewhere — another book, in person — logged here by
    // hand): provide the line/odds you actually got directly. Required for Spread/Total
    // whenever PlayerPropLineId isn't given; for PlayerProp also requires PlayerId and
    // MarketKey since there's no line row to derive the player/market from.
    decimal? ManualLine,
    int? ManualOdds,
    int? PlayerId,                // PlayerProp manual mode only
    string? MarketKey,            // PlayerProp manual mode only — e.g. "player_receptions"
    string? MarketLabel           // manual mode only — overrides the label PropMarketMapping would derive
);

public record BetResponse(
    int Id,
    string Kind,
    bool IsManual,               // placed on another book and logged here by hand
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
    string PlacedAt,             // ISO 8601

    // Closing-line value — how the market moved between when this bet was placed and
    // kickoff. Read live off the same line/prop row (which stops changing once the game
    // starts), not stored: null until the game has actually started.
    decimal? ClosingLine,
    int? ClosingOdds,
    decimal? ClvPct              // + = you beat the close (market moved toward your side); - = it moved away
);
