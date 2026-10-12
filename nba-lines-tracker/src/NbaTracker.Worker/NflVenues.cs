namespace NbaTracker.Worker;

// Static by design — NFL stadiums essentially never change mid-season, and the
// handful of relocations/new builds that do happen (every few years) are a known,
// deliberate edit here, not something worth an API call to keep "live" synced.
//
// Retractable-roof stadiums (Cowboys, Falcons, Texans, Colts, Cardinals) are treated
// as weather-immune: teams almost always close the roof for anything that would
// actually affect play (real rain/snow/wind), so for "should we flag this game as a
// weather risk" purposes they behave like domes. A simplification, not a guarantee.
public static class NflVenues
{
    public readonly record struct Venue(decimal Latitude, decimal Longitude, bool IsOutdoor);

    public static readonly Dictionary<string, Venue> ByTeamAbbreviation = new()
    {
        ["ARI"] = new(33.5276m, -112.2626m, false), // State Farm Stadium — retractable
        ["ATL"] = new(33.7554m, -84.4008m, false),  // Mercedes-Benz Stadium — retractable
        ["BAL"] = new(39.2780m, -76.6227m, true),   // M&T Bank Stadium
        ["BUF"] = new(42.7738m, -78.7870m, true),   // Highmark Stadium — lake-effect snow territory
        ["CAR"] = new(35.2258m, -80.8528m, true),   // Bank of America Stadium
        ["CHI"] = new(41.8623m, -87.6167m, true),   // Soldier Field
        ["CIN"] = new(39.0955m, -84.5161m, true),   // Paycor Stadium
        ["CLE"] = new(41.5061m, -81.6995m, true),   // Huntington Bank Field — lake-effect
        ["DAL"] = new(32.7473m, -97.0945m, false),  // AT&T Stadium — retractable
        ["DEN"] = new(39.7439m, -105.0201m, true),  // Empower Field at Mile High
        ["DET"] = new(42.3400m, -83.0456m, false),  // Ford Field — dome
        ["GB"]  = new(44.5013m, -88.0622m, true),   // Lambeau Field — the benchmark cold-weather game
        ["HOU"] = new(29.6847m, -95.4107m, false),  // NRG Stadium — retractable
        ["IND"] = new(39.7601m, -86.1639m, false),  // Lucas Oil Stadium — retractable
        ["JAX"] = new(30.3239m, -81.6373m, true),   // EverBank Stadium
        ["KC"]  = new(39.0489m, -94.4839m, true),   // GEHA Field at Arrowhead
        ["LV"]  = new(36.0909m, -115.1833m, false), // Allegiant Stadium — dome
        ["LAC"] = new(33.9535m, -118.3392m, false), // SoFi Stadium — fixed roof
        ["LAR"] = new(33.9535m, -118.3392m, false), // SoFi Stadium — fixed roof
        ["MIA"] = new(25.9580m, -80.2389m, true),   // Hard Rock Stadium
        ["MIN"] = new(44.9738m, -93.2577m, false),  // U.S. Bank Stadium — dome
        ["NE"]  = new(42.0909m, -71.2643m, true),   // Gillette Stadium
        ["NO"]  = new(29.9511m, -90.0812m, false),  // Caesars Superdome — dome
        ["NYG"] = new(40.8135m, -74.0745m, true),   // MetLife Stadium
        ["NYJ"] = new(40.8135m, -74.0745m, true),   // MetLife Stadium
        ["PHI"] = new(39.9008m, -75.1675m, true),   // Lincoln Financial Field
        ["PIT"] = new(40.4468m, -80.0158m, true),   // Acrisure Stadium
        ["SF"]  = new(37.4030m, -121.9700m, true),  // Levi's Stadium
        ["SEA"] = new(47.5952m, -122.3316m, true),  // Lumen Field — mostly covered but open-air
        ["TB"]  = new(27.9759m, -82.5033m, true),   // Raymond James Stadium
        ["TEN"] = new(36.1665m, -86.7713m, true),   // Nissan Stadium
        ["WAS"] = new(38.9077m, -76.8645m, true),   // Commanders Field (formerly FedExField)
    };

    public static Venue? TryGetVenue(string teamAbbreviation) =>
        ByTeamAbbreviation.TryGetValue(teamAbbreviation, out var venue) ? venue : null;
}
