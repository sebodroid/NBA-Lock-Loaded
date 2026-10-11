namespace NbaTracker.Api;

public static class TeamLogoUrl
{
    public static string? For(string sport, string nbaApiId) =>
        sport == "CFB"
            ? $"https://highlightly.net/american-football/images/teams/{nbaApiId}.png"
            : null;
}
