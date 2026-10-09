using System.Runtime.InteropServices;

namespace NbaTracker.Api;

// "Today" for this app always means the Eastern-time calendar day — every sync (schedule,
// scores, odds) is bucketed that way (see NflSyncOrchestrator/GameMatchingService in the
// Worker), so the API has to agree, or "today" silently disagrees with the Worker for
// several hours a day: raw UTC rolls over to the next calendar day at 8 PM ET, so a plain
// DateOnly.FromDateTime(DateTime.UtcNow) call thinks tomorrow has already started while a
// game correctly filed under today (Eastern) is still hours from kickoff — that game then
// vanishes from every "today" view until Eastern time also crosses midnight.
public static class ApiClock
{
    private static readonly TimeZoneInfo EasternTime =
        TimeZoneInfo.FindSystemTimeZoneById(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "Eastern Standard Time"
                : "America/New_York");

    public static DateOnly Today =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EasternTime));
}
