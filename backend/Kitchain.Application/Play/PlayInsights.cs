using Kitchain.Domain.Play;

namespace Kitchain.Application.Play;

public sealed record PlayPlayerInsights(Guid PlayerId, string DisplayName, int GamesPlayed, int Wins,
    int Losses, int DistinctTeammates, int DistinctOpponents, bool IsRemoved = false);

public sealed record PlayInsights(int TotalPlayers, int NumberOfCourts, int CompletedGames,
    int PlayerAppearances, int? RecordedRallies, int? TaggedRallies, IReadOnlyList<PlayPlayerInsights> Players)
{
    internal static PlayInsights From(PlaySession session, IReadOnlyList<PlayMatchSummary> completed)
    {
        var scoring = session.Mode == PlaySessionMode.LiveScoring;
        var players = session.Players.Select(player =>
        {
            var appearances = completed.Where(m => m.Players.Any(p => p.PlayerId == player.Id))
                .Select(m => new { Match = m, Team = m.Players.Single(p => p.PlayerId == player.Id).Team }).ToArray();
            return new PlayPlayerInsights(player.Id, player.DisplayName, appearances.Length,
                appearances.Count(a => a.Match.Winner == a.Team),
                appearances.Count(a => a.Match.Winner.HasValue && a.Match.Winner != a.Team),
                appearances.SelectMany(a => a.Match.Players.Where(p => p.Team == a.Team && p.PlayerId != player.Id))
                    .Select(p => p.PlayerId).Distinct().Count(),
                appearances.SelectMany(a => a.Match.Players.Where(p => p.Team != a.Team))
                    .Select(p => p.PlayerId).Distinct().Count(), player.IsRemoved);
        }).OrderByDescending(p => p.GamesPlayed).ThenByDescending(p => p.Wins)
            .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.DisplayName, StringComparer.Ordinal).ThenBy(p => p.PlayerId).ToArray();
        return new(session.Players.Count(p => !p.IsRemoved), session.NumberOfCourts, completed.Count,
            completed.Sum(m => m.Players.Count),
            scoring ? completed.Sum(m => m.TotalRallies ?? 0) : null,
            scoring ? completed.Sum(m => m.TaggedRallies ?? 0) : null, players);
    }
}
