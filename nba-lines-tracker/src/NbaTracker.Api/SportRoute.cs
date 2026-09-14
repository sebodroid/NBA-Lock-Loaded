namespace NbaTracker.Api;

public static class SportRoute
{
    private static readonly HashSet<string> Valid =
        new(StringComparer.OrdinalIgnoreCase) { "NBA", "NFL", "MLB" };

    public static bool TryNormalize(string sport, out string normalized)
    {
        normalized = sport.ToUpperInvariant();
        return Valid.Contains(normalized);
    }
}
