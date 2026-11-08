namespace NbaTracker.Data.Entities;

// One row per (Game, Player, Market) the first time it ever qualified as a Hot Bet —
// never updated after creation, same "locked to the number you saw" philosophy as Bet's
// own snapshot fields. This is what makes the accuracy tracker honest: it records what
// we actually said BEFORE the outcome was known, so grading it afterward (live, against
// the real box score — see PropStatResolver, same pattern Bet grading already uses) is a
// real test of whether the confidence number means anything, not something that can be
// retroactively massaged.
public class PredictionSnapshot
{
    public int Id { get; set; }
    public string Sport { get; set; } = null!;
    public int GameId { get; set; }
    public int PlayerId { get; set; }
    public string MarketKey { get; set; } = null!;
    public BetSide Side { get; set; }             // Over or Under — which direction this snapshot predicted
    public decimal Line { get; set; }
    public decimal ConfidencePct { get; set; }   // whatever headline number Hot Bets showed at the time
    public DateTime CreatedAt { get; set; }

    public Game Game { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
