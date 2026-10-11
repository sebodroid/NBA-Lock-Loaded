using NbaTracker.Data;
using NbaTracker.Data.Entities;
using NbaTracker.Worker.Models.OddsApi;

namespace NbaTracker.Worker.Services;

public static class CfbTeamProps
{
    public static List<TeamPropLine> BuildLines(
        Game game, OddsApiEvent propsEvent, bool swapped, IReadOnlyList<string> bookmakerPriority)
    {
        var lines = new List<TeamPropLine>();
        var now = DateTime.UtcNow;

        int? ResolveTeam(string? name)
        {
            if (name is null) return null;
            if (name == propsEvent.HomeTeam) return swapped ? game.AwayTeamId : game.HomeTeamId;
            if (name == propsEvent.AwayTeam) return swapped ? game.HomeTeamId : game.AwayTeamId;
            return null;
        }

        foreach (var marketKey in TeamPropMarkets.Keys)
        {
            OddsApiBookmaker? book = null;
            OddsApiMarket? market = null;
            foreach (var bookKey in bookmakerPriority)
            {
                var candidate = propsEvent.Bookmakers.FirstOrDefault(b => b.Key == bookKey);
                var candidateMarket = candidate?.Markets.FirstOrDefault(m => m.Key == marketKey && m.Outcomes.Count > 0);
                if (candidateMarket is not null)
                {
                    book = candidate;
                    market = candidateMarket;
                    break;
                }
            }
            if (book is null || market is null) continue;

            var marketLines = new List<TeamPropLine>();
            bool valid = true;

            foreach (var outcome in market.Outcomes)
            {
                bool overUnder = outcome.Name is "Over" or "Under";
                int? teamId;

                if (overUnder)
                {
                    bool teamScoped = !string.IsNullOrEmpty(outcome.Description);
                    teamId = teamScoped ? ResolveTeam(outcome.Description) : null;
                    if (outcome.Point is null || (teamScoped && teamId is null)) { valid = false; break; }
                }
                else
                {
                    teamId = ResolveTeam(outcome.Name);
                    if (teamId is null) { valid = false; break; }
                }

                marketLines.Add(new TeamPropLine
                {
                    GameId        = game.Id,
                    MarketKey     = marketKey,
                    Side          = overUnder ? outcome.Name : "Team",
                    TeamId        = teamId,
                    Line          = outcome.Point,
                    Price         = (int)Math.Round(outcome.Price),
                    Bookmaker     = book.Key,
                    LineTimestamp = now,
                    UpdatedAt     = now
                });
            }

            if (valid) lines.AddRange(marketLines);
        }

        return lines;
    }
}
