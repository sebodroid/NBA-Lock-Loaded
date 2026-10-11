using NbaTracker.Worker;

namespace NbaTracker.Worker.Tests;

public class CfbGapDetectionTests
{
    private static readonly DateOnly SeasonStart = new(2026, 8, 29);
    private static readonly DateOnly WindowStart = new(2026, 9, 13);

    private static DateTime Utc(int month, int day, int hour) => new(2026, month, day, hour, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NoHistory_BackfillsTheWholeSeasonSoFar()
    {
        var missing = CfbWorker.FindDatesToBackfill([], SeasonStart, WindowStart);

        Assert.Equal(15, missing.Count);
        Assert.Equal(SeasonStart, missing[0]);
        Assert.Equal(new DateOnly(2026, 9, 12), missing[^1]);
    }

    [Fact]
    public void OnlyRunsInsideTheWindow_StillBackfillsEverythingBefore()
    {
        var runs = Enumerable.Range(13, 7)
            .Select(d => (new DateOnly(2026, 9, d), Utc(9, 19, 15)))
            .ToList();

        var missing = CfbWorker.FindDatesToBackfill(runs, SeasonStart, WindowStart);

        Assert.Equal(15, missing.Count);
    }

    [Fact]
    public void ADateWithAPostDayRun_IsNotRevisited()
    {
        var runs = new[] { (new DateOnly(2026, 9, 5), Utc(9, 6, 12)) };

        var missing = CfbWorker.FindDatesToBackfill(runs, SeasonStart, WindowStart);

        Assert.DoesNotContain(new DateOnly(2026, 9, 5), missing);
        Assert.Equal(14, missing.Count);
    }

    [Fact]
    public void ARunOnTheGameDayItself_DoesNotCount()
    {
        var runs = new[] { (new DateOnly(2026, 9, 5), Utc(9, 5, 12)) };

        var missing = CfbWorker.FindDatesToBackfill(runs, SeasonStart, WindowStart);

        Assert.Contains(new DateOnly(2026, 9, 5), missing);
    }

    [Fact]
    public void DatesInsideTheWindow_AreLeftToTheRollingWindow()
    {
        var missing = CfbWorker.FindDatesToBackfill([], SeasonStart, WindowStart);

        Assert.DoesNotContain(WindowStart, missing);
        Assert.All(missing, d => Assert.True(d < WindowStart));
    }

    [Fact]
    public void BeforeTheSeasonStarts_NothingToBackfill()
    {
        var missing = CfbWorker.FindDatesToBackfill([], SeasonStart, new DateOnly(2026, 7, 1));

        Assert.Empty(missing);
    }

    [Fact]
    public void FullyCoveredSeason_ReturnsNothing()
    {
        var runs = Enumerable.Range(0, 15)
            .Select(i => (SeasonStart.AddDays(i), Utc(9, 19, 15)))
            .ToList();

        Assert.Empty(CfbWorker.FindDatesToBackfill(runs, SeasonStart, WindowStart));
    }
}
