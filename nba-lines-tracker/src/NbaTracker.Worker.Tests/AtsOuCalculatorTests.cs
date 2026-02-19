using NbaTracker.Data.Entities;
using NbaTracker.Worker.Services;

namespace NbaTracker.Worker.Tests;

public class AtsOuCalculatorTests
{
    // -----------------------------------------------------------------------
    // CalculateFavoriteAts — Cover cases
    // -----------------------------------------------------------------------

    [Fact]
    public void CalculateFavoriteAts_HomeIsFavorite_WinsByMoreThanSpread_ReturnsCover()
    {
        // Home wins 110-100 (margin 10), spread 7.5 — 10 > 7.5 => Cover
        var result = AtsOuCalculator.CalculateFavoriteAts(
            homeScore: 110,
            awayScore: 100,
            spread: 7.5m,
            favoriteTeamId: 1,
            homeTeamId: 1);

        Assert.Equal(AtsResult.Cover, result);
    }

    // -----------------------------------------------------------------------
    // CalculateFavoriteAts — Loss cases
    // -----------------------------------------------------------------------

    [Fact]
    public void CalculateFavoriteAts_HomeIsFavorite_WinsByLessThanSpread_ReturnsLoss()
    {
        // Home wins 107-100 (margin 7), spread 7.5 — 7 < 7.5 => Loss
        var result = AtsOuCalculator.CalculateFavoriteAts(
            homeScore: 107,
            awayScore: 100,
            spread: 7.5m,
            favoriteTeamId: 1,
            homeTeamId: 1);

        Assert.Equal(AtsResult.Loss, result);
    }

    [Fact]
    public void CalculateFavoriteAts_HomeIsFavorite_LosesOutright_ReturnsLoss()
    {
        // Home (fav) tied 100-100, spread 3.5 — margin 0 < 3.5 => Loss
        var result = AtsOuCalculator.CalculateFavoriteAts(
            homeScore: 100,
            awayScore: 100,
            spread: 3.5m,
            favoriteTeamId: 1,
            homeTeamId: 1);

        Assert.Equal(AtsResult.Loss, result);
    }

    // -----------------------------------------------------------------------
    // CalculateFavoriteAts — Push cases
    // -----------------------------------------------------------------------

    [Fact]
    public void CalculateFavoriteAts_HomeIsFavorite_WinsByExactSpread_ReturnsPush()
    {
        // Home wins 108-100 (margin 8), spread 8.0 — 8 == 8 => Push
        var result = AtsOuCalculator.CalculateFavoriteAts(
            homeScore: 108,
            awayScore: 100,
            spread: 8.0m,
            favoriteTeamId: 1,
            homeTeamId: 1);

        Assert.Equal(AtsResult.Push, result);
    }

    // -----------------------------------------------------------------------
    // CalculateFavoriteAts — Away is favorite
    // -----------------------------------------------------------------------

    [Fact]
    public void CalculateFavoriteAts_AwayIsFavorite_WinsOutright_ReturnsCover()
    {
        // Away (fav, id=2) wins 110-100 (fav margin 10 > 7.5) => Cover
        var result = AtsOuCalculator.CalculateFavoriteAts(
            homeScore: 100,
            awayScore: 110,
            spread: 7.5m,
            favoriteTeamId: 2,
            homeTeamId: 1);

        Assert.Equal(AtsResult.Cover, result);
    }

    // -----------------------------------------------------------------------
    // DeriveBothSides
    // -----------------------------------------------------------------------

    [Fact]
    public void DeriveBothSides_HomeIsFavorite_Cover_ReturnsHomeCoverAwayLoss()
    {
        var (homeAts, awayAts) = AtsOuCalculator.DeriveBothSides(
            favoriteResult: AtsResult.Cover,
            favoriteTeamId: 1,
            homeTeamId: 1);

        Assert.Equal(AtsResult.Cover, homeAts);
        Assert.Equal(AtsResult.Loss, awayAts);
    }

    [Fact]
    public void DeriveBothSides_Push_BothSidesArePush()
    {
        // Push flips to Push — both home and away get Push regardless of who is favorite
        var (homeAts, awayAts) = AtsOuCalculator.DeriveBothSides(
            favoriteResult: AtsResult.Push,
            favoriteTeamId: 2,
            homeTeamId: 1);

        Assert.Equal(AtsResult.Push, homeAts);
        Assert.Equal(AtsResult.Push, awayAts);
    }

    // -----------------------------------------------------------------------
    // CalculateOu
    // -----------------------------------------------------------------------

    [Fact]
    public void CalculateOu_CombinedExceedsTotal_ReturnsOver()
    {
        // 115 + 110 = 225 > 220.0
        var result = AtsOuCalculator.CalculateOu(homeScore: 115, awayScore: 110, total: 220.0m);
        Assert.Equal(OuResult.Over, result);
    }

    [Fact]
    public void CalculateOu_CombinedBelowTotal_ReturnsUnder()
    {
        // 105 + 110 = 215 < 220.0
        var result = AtsOuCalculator.CalculateOu(homeScore: 105, awayScore: 110, total: 220.0m);
        Assert.Equal(OuResult.Under, result);
    }

    [Fact]
    public void CalculateOu_CombinedEqualsTotal_ReturnsPush()
    {
        // 110 + 110 = 220 == 220.0
        var result = AtsOuCalculator.CalculateOu(homeScore: 110, awayScore: 110, total: 220.0m);
        Assert.Equal(OuResult.Push, result);
    }
}
