using NbaTracker.Data.Entities;

namespace NbaTracker.Worker.Services;

/// <summary>
/// Pure static ATS and O/U calculation logic. No HTTP or DB dependencies.
/// FavoriteTeamId drives all calculations — never infer favorite from spread sign.
/// </summary>
public static class AtsOuCalculator
{
    /// <summary>
    /// Returns the favorite's ATS result (Cover/Loss/Push).
    /// </summary>
    /// <param name="homeScore">Final home team score.</param>
    /// <param name="awayScore">Final away team score.</param>
    /// <param name="spread">Spread from the favorite's perspective (always positive, absolute value).</param>
    /// <param name="favoriteTeamId">The team ID of the favorite.</param>
    /// <param name="homeTeamId">The team ID of the home team.</param>
    public static AtsResult CalculateFavoriteAts(
        int homeScore, int awayScore, decimal spread, int favoriteTeamId, int homeTeamId)
    {
        bool homeIsFavorite = favoriteTeamId == homeTeamId;
        int favoriteScore = homeIsFavorite ? homeScore : awayScore;
        int underdogScore = homeIsFavorite ? awayScore : homeScore;
        decimal margin = favoriteScore - underdogScore;

        if (margin > spread) return AtsResult.Cover;
        if (margin < spread) return AtsResult.Loss;
        return AtsResult.Push;
    }

    /// <summary>
    /// Derives both home and away ATS results from the favorite's result.
    /// Push flips to Push — both sides get Push when margin equals spread exactly.
    /// </summary>
    public static (AtsResult HomeAts, AtsResult AwayAts) DeriveBothSides(
        AtsResult favoriteResult, int favoriteTeamId, int homeTeamId)
    {
        bool homeIsFavorite = favoriteTeamId == homeTeamId;

        static AtsResult Flip(AtsResult r) => r switch
        {
            AtsResult.Cover => AtsResult.Loss,
            AtsResult.Loss  => AtsResult.Cover,
            _               => AtsResult.Push   // Push flips to Push
        };

        return homeIsFavorite
            ? (favoriteResult, Flip(favoriteResult))
            : (Flip(favoriteResult), favoriteResult);
    }

    /// <summary>
    /// Returns the O/U result — Over when combined score exceeds total, Under when less, Push when equal.
    /// </summary>
    public static OuResult CalculateOu(int homeScore, int awayScore, decimal total)
    {
        decimal combined = homeScore + awayScore;
        if (combined > total) return OuResult.Over;
        if (combined < total) return OuResult.Under;
        return OuResult.Push;
    }
}
