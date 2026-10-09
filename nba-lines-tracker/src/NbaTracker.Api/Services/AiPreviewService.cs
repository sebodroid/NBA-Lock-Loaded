using System.Linq;
using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.EntityFrameworkCore;
using NbaTracker.Data;
using NbaTracker.Data.Entities;

namespace NbaTracker.Api.Services;

public class AiPreviewService
{
    private readonly NbaTrackerDbContext _db;
    private readonly AnthropicClient _client;
    private readonly ILogger<AiPreviewService> _logger;

    // Upcoming/live games can have their line move — regenerate periodically.
    // FINAL games never change, so they're cached forever once generated.
    private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(3);

    private const string SystemPrompt = """
        You are a sports betting analyst writing a short, data-driven game preview for a
        subscription analytics platform. You are given structured stats for two teams and
        the current betting line. Write 2-3 short paragraphs covering: what the numbers say
        about each team's tendencies (ATS, over/under, recent form), how the current line
        relates to those tendencies, and head-to-head history if relevant.

        Rules:
        - Use ONLY the data provided. Do not invent injuries, weather, coaching changes,
          player news, or any fact not given to you.
        - If a team has played fewer than 4 games this season, explicitly say the sample
          size is small and the trend is not yet reliable.
        - Never use guarantee language ("lock", "guaranteed", "can't lose", "sure thing").
          Frame everything as informational context, not instructions to bet.
        - End with exactly one sentence reminding the reader this is not financial advice
          and betting involves risk.
        - Keep the total response under 200 words.
        """;

    public AiPreviewService(NbaTrackerDbContext db, IConfiguration config, ILogger<AiPreviewService> logger)
    {
        _db = db;
        _logger = logger;
        var apiKey = config["Anthropic:ApiKey"]
            ?? throw new InvalidOperationException("Anthropic__ApiKey configuration is required.");
        _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<string> GetOrGeneratePreviewAsync(Game game, CancellationToken ct)
    {
        var existing = await _db.GamePreviews.FirstOrDefaultAsync(p => p.GameId == game.Id, ct);

        bool isStale = existing is null
            || (game.Status != "FINAL" && DateTime.UtcNow - existing.GeneratedAt > StaleAfter);

        if (existing is not null && !isStale)
            return existing.Text;

        var text = await GenerateAsync(game, ct);

        if (existing is null)
        {
            _db.GamePreviews.Add(new GamePreview
            {
                GameId      = game.Id,
                Text        = text,
                GeneratedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Text        = text;
            existing.GeneratedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return text;
    }

    private async Task<string> GenerateAsync(Game game, CancellationToken ct)
    {
        var homeTeam = await _db.Teams.FirstAsync(t => t.Id == game.HomeTeamId, ct);
        var awayTeam = await _db.Teams.FirstAsync(t => t.Id == game.AwayTeamId, ct);
        var line = await _db.GameLines.FirstOrDefaultAsync(l => l.GameId == game.Id, ct);

        var statsStart = SeasonHelper.StatsStartDate(game.Sport, ApiClock.Today);
        var homeSummary = await BuildTeamSummaryAsync(homeTeam, statsStart, ct);
        var awaySummary = await BuildTeamSummaryAsync(awayTeam, statsStart, ct);

        var h2hCount = await _db.Games.CountAsync(g =>
            g.Sport == game.Sport && g.Status == "FINAL" && g.GameDate >= statsStart &&
            ((g.HomeTeamId == homeTeam.Id && g.AwayTeamId == awayTeam.Id) ||
             (g.HomeTeamId == awayTeam.Id && g.AwayTeamId == homeTeam.Id)), ct);

        var prompt = $"""
            Sport: {game.Sport}
            Matchup: {homeTeam.Name} (home) vs {awayTeam.Name} (away)
            Current line: {(line?.Spread is { } s ? $"spread {s}" : "no spread available")}, \
            {(line?.Total is { } t ? $"total {t}" : "no total available")}

            {homeTeam.Name} this season: {homeSummary}
            {awayTeam.Name} this season: {awaySummary}

            Head-to-head meetings this season: {h2hCount}
            """;

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = "claude-opus-5",
            MaxTokens = 500,
            System = SystemPrompt,
            Messages = [new() { Role = Role.User, Content = prompt }]
        });

        var text = string.Concat(
            response.Content.Select(b => b.Value).OfType<TextBlock>().Select(b => b.Text));

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Claude returned an empty preview.");

        return text.Trim();
    }

    private async Task<string> BuildTeamSummaryAsync(Team team, DateOnly statsStart, CancellationToken ct)
    {
        var homeGames = await _db.Games
            .Where(g => g.HomeTeamId == team.Id && g.Status == "FINAL" && g.GameDate >= statsStart)
            .Include(g => g.GameResult)
            .ToListAsync(ct);
        var awayGames = await _db.Games
            .Where(g => g.AwayTeamId == team.Id && g.Status == "FINAL" && g.GameDate >= statsStart)
            .Include(g => g.GameResult)
            .ToListAsync(ct);

        int gamesPlayed = homeGames.Count + awayGames.Count;
        int wins = homeGames.Count(g => g.HomeScore > g.AwayScore)
                 + awayGames.Count(g => g.AwayScore > g.HomeScore);
        int losses = gamesPlayed - wins;

        int atsCovers = homeGames.Count(g => g.GameResult?.HomeAtsResult == AtsResult.Cover)
                      + awayGames.Count(g => g.GameResult?.AwayAtsResult == AtsResult.Cover);
        int atsLosses = homeGames.Count(g => g.GameResult?.HomeAtsResult == AtsResult.Loss)
                      + awayGames.Count(g => g.GameResult?.AwayAtsResult == AtsResult.Loss);

        var allGames = homeGames.Concat(awayGames).ToList();
        int overs  = allGames.Count(g => g.GameResult?.OuResult == OuResult.Over);
        int unders = allGames.Count(g => g.GameResult?.OuResult == OuResult.Under);

        return $"{gamesPlayed} games played, {wins}-{losses} record, " +
               $"{atsCovers}-{atsLosses} against the spread, {overs}-{unders} over/under.";
    }
}
