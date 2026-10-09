namespace NbaTracker.Data.Entities;

// What the bet is against — Spread/Total use the game's own line, PlayerProp uses a
// specific PlayerPropLine row.
public enum BetKind { PlayerProp, Spread, Total }

// "Over"/"Under" for PlayerProp and Total; "Home"/"Away" (which side was picked) for Spread.
public enum BetSide { Over, Under, Home, Away }

public class Bet
{
    public int Id { get; set; }
    public string Sport { get; set; } = null!;
    public int GameId { get; set; }
    public BetKind Kind { get; set; }

    // Set only when Kind == PlayerProp. PlayerPropLineId is null for a manually-entered
    // bet (placed on another book, logged here by hand) — MarketKey is what grading and
    // CLV actually key off, so it's set either way, synced or manual.
    public int? PlayerPropLineId { get; set; }
    public int? PlayerId { get; set; }
    public string? MarketKey { get; set; }   // e.g. "player_receptions" — PlayerProp only

    // True when this bet's line/odds were typed in by hand rather than snapshotted from
    // a line this app actually tracked (a bet placed on a different book). Grading and
    // CLV work identically either way — CLV just falls back to whatever matching line
    // this app has synced for the same game/market, if any, rather than the bet's own.
    public bool IsManual { get; set; }

    // Everything below is a snapshot taken at bet placement — sportsbooks lock you to
    // the number you saw, not whatever the line moves to afterward, so grading must
    // replay against these values rather than the (possibly since-updated) GameLine
    // or PlayerPropLine row.
    public string MarketLabel { get; set; } = null!;   // "Rushing Yards", "Spread", "Total"
    public string? TeamAbbreviation { get; set; }        // Spread only — which team was picked
    public decimal LineAtBet { get; set; }
    public BetSide Side { get; set; }
    public int OddsAtBet { get; set; }
    public decimal StakeAmount { get; set; }
    public decimal ToWinAmount { get; set; }              // profit if it wins, computed at placement
    public DateTime PlacedAt { get; set; }

    public Game Game { get; set; } = null!;
    public PlayerPropLine? PlayerPropLine { get; set; }
    public Player? Player { get; set; }
}
