namespace NbaTracker.Api.Models;

// One bettable thing, from either pool — a player prop (reusing Hot Bets' already-vetted
// candidates) or a team total lean (new: see ParlayEndpoints.GetGameTotalLean). ConfidencePct
// is OUR OWN stat-based estimate, not the bookmaker's implied probability from Odds —
// that's deliberate, since the point of this feature is ranking by "our read on how
// likely this is," the same number already driving Hot Bets and the prop estimates.
public record ParlayLeg(
    string Kind,              // "PlayerProp" | "Total"
    string Description,
    int GameId,
    string GameLabel,
    decimal ConfidencePct,
    int Odds,                 // real American odds for this leg's side — used for payout math only
    int? PlayerId,
    int? PlayerPropLineId
);

public record ParlaySuggestion(
    List<ParlayLeg> Legs,
    decimal CombinedProbabilityPct,   // product of each leg's own ConfidencePct — assumes
                                       // independence, a simplification (legs can be
                                       // positively correlated, e.g. a team total and its
                                       // own player's prop), which only ever UNDERSTATES
                                       // the real combined hit chance, never overstates it
    int CombinedOdds                  // real parlay payout odds, from multiplying decimal odds
);
