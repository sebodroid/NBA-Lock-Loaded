using Microsoft.EntityFrameworkCore;
using NbaTracker.Api.Models;
using NbaTracker.Data;
using NbaTracker.Data.Entities;

namespace NbaTracker.Api.Endpoints;

public static class GameEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/today", GetTodayMatchupsAsync);
    }

    // GET /api/games/today — today's games with season stats and H2H history for each matchup
    private static async Task<IResult> GetTodayMatchupsAsync(
        NbaTrackerDbContext db,
        CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Today's games (all statuses except POSTPONED)
        var todayGames = await db.Games
            .Include(g => g.HomeTeam)
            .Include(g => g.AwayTeam)
            .Include(g => g.GameLine)
            .Include(g => g.GameResult)
            .Where(g => g.GameDate == today && g.Status != "POSTPONED")
            .ToListAsync(ct);

        if (todayGames.Count == 0)
            return Results.Ok(new List<TodayMatchupResponse>());

        // Collect all unique team IDs involved in today's games
        var teamIds = todayGames
            .SelectMany(g => new[] { g.HomeTeamId, g.AwayTeamId })
            .Distinct()
            .ToHashSet();

        // Load all FINAL games for these teams this season in one query
        // Season is determined from today's date: month >= 10 → current year, else year - 1
        int seasonYear = today.Month >= 10 ? today.Year : today.Year - 1;
        string season = $"{seasonYear}-{(seasonYear + 1) % 100:D2}";

        var seasonGames = await db.Games
            .Include(g => g.GameLine)
            .Include(g => g.GameResult)
            .Where(g => g.Season == season
                     && g.Status == "FINAL"
                     && (teamIds.Contains(g.HomeTeamId) || teamIds.Contains(g.AwayTeamId)))
            .ToListAsync(ct);

        var result = todayGames.Select(game =>
        {
            int homeId = game.HomeTeamId;
            int awayId = game.AwayTeamId;

            // Season stats for home team
            var homeSeasonGames = seasonGames
                .Where(g => g.HomeTeamId == homeId || g.AwayTeamId == homeId)
                .ToList();
            var homeStats = BuildSeasonStats(homeSeasonGames, homeId);

            // Season stats for away team
            var awaySeasonGames = seasonGames
                .Where(g => g.HomeTeamId == awayId || g.AwayTeamId == awayId)
                .ToList();
            var awayStats = BuildSeasonStats(awaySeasonGames, awayId);

            // Head-to-head: all FINAL games between these two teams this season, newest first
            var h2hGames = seasonGames
                .Where(g => (g.HomeTeamId == homeId && g.AwayTeamId == awayId)
                         || (g.HomeTeamId == awayId && g.AwayTeamId == homeId))
                .OrderByDescending(g => g.GameDate)
                .Select(g => new H2HGameEntry(
                    g.Id,
                    g.GameDate.ToString("yyyy-MM-dd"),
                    g.HomeTeamId,
                    g.HomeScore,
                    g.AwayScore,
                    g.GameLine?.Spread,
                    g.GameLine?.Total,
                    g.GameResult?.HomeAtsResult?.ToString(),
                    g.GameResult?.AwayAtsResult?.ToString(),
                    g.GameResult?.OuResult?.ToString()
                ))
                .ToList();

            return new TodayMatchupResponse(
                game.Id,
                game.Status,
                homeId,
                game.HomeTeam.Name,
                game.HomeTeam.Abbreviation,
                awayId,
                game.AwayTeam.Name,
                game.AwayTeam.Abbreviation,
                game.GameLine?.Spread,
                game.GameLine?.FavoriteTeamId,
                game.GameLine?.Total,
                game.GameLine?.Bookmaker,
                homeStats,
                awayStats,
                h2hGames
            );
        }).ToList();

        return Results.Ok(result);
    }

    private static TeamSeasonStats BuildSeasonStats(List<Game> games, int teamId)
    {
        var homeGames = games.Where(g => g.HomeTeamId == teamId).ToList();
        var awayGames = games.Where(g => g.AwayTeamId == teamId).ToList();

        int wins = homeGames.Count(g => g.HomeScore > g.AwayScore)
                 + awayGames.Count(g => g.AwayScore > g.HomeScore);
        int losses = homeGames.Count(g => g.HomeScore < g.AwayScore)
                   + awayGames.Count(g => g.AwayScore < g.HomeScore);

        int atsCovers = homeGames.Count(g => g.GameResult?.HomeAtsResult == AtsResult.Cover)
                      + awayGames.Count(g => g.GameResult?.AwayAtsResult == AtsResult.Cover);
        int atsLosses = homeGames.Count(g => g.GameResult?.HomeAtsResult == AtsResult.Loss)
                      + awayGames.Count(g => g.GameResult?.AwayAtsResult == AtsResult.Loss);
        int atsPushes = homeGames.Count(g => g.GameResult?.HomeAtsResult == AtsResult.Push)
                      + awayGames.Count(g => g.GameResult?.AwayAtsResult == AtsResult.Push);

        // O/U is per-game (same result for both teams) — union all games, count once each
        var allGames = homeGames.Concat(awayGames).ToList();
        int ouOvers   = allGames.Count(g => g.GameResult?.OuResult == OuResult.Over);
        int ouUnders  = allGames.Count(g => g.GameResult?.OuResult == OuResult.Under);
        int ouPushes  = allGames.Count(g => g.GameResult?.OuResult == OuResult.Push);

        return new TeamSeasonStats(
            homeGames.Count + awayGames.Count,
            wins, losses,
            atsCovers, atsLosses, atsPushes,
            ouOvers, ouUnders, ouPushes
        );
    }
}
