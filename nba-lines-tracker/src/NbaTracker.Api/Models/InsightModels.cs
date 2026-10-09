namespace NbaTracker.Api.Models;

public record InsightResponse(
    string Category,   // "team-ats" | "team-ou" | "market-trend"
    string Text,
    int SampleSize      // games behind this insight — always shown alongside it in the UI
);
