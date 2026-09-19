namespace NbaTracker.Api;

// The frontend builds NFL/NBA/MLB logo URLs from the team abbreviation (ESPN's CDN), which
// doesn't work for college football: ESPN's college logos are keyed by numeric ID, and
// CFB abbreviations aren't a stable key. CFB teams come from Highlightly, which serves
// each logo at a URL built from the same team ID we store as NbaApiId — so the API hands
// the client that URL directly. Returns null for sports that keep the abbreviation scheme.
public static class TeamLogoUrl
{
    public static string? For(string sport, string nbaApiId) =>
        sport == "CFB"
            ? $"https://highlightly.net/american-football/images/teams/{nbaApiId}.png"
            : null;
}
