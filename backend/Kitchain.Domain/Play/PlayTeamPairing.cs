using System.Security.Cryptography;
using System.Text;

namespace Kitchain.Domain.Play;

/// <summary>Arranges an already selected fair group; never selects different players.</summary>
internal static class PlayTeamPairing
{
    public static PlaySessionPlayer[] Recommend(Guid sessionId, IReadOnlyList<PlaySessionPlayer> selected,
        IEnumerable<PlayMatch> history, Guid seed)
    {
        if (selected.Count != 4) return selected.ToArray();
        var matches = history.Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.StartedAt).ThenByDescending(m => m.Id).ToArray();
        var recent = selected.ToDictionary(p => p.Id, p => matches.Where(m => m.Players.Any(slot => slot.PlayerId == p.Id))
            .Take(3).Select(m => m.Id).ToHashSet());
        (int Total, int Recent) Count(int a, int b, bool teammates)
        {
            var first = selected[a].Id;
            var second = selected[b].Id;
            var meetings = matches.Where(m =>
            {
                var one = m.Players.SingleOrDefault(p => p.PlayerId == first);
                var two = m.Players.SingleOrDefault(p => p.PlayerId == second);
                return one is not null && two is not null && (one.Team == two.Team) == teammates;
            }).ToArray();
            return (meetings.Length, meetings.Count(m => recent[first].Contains(m.Id) || recent[second].Contains(m.Id)));
        }
        int[][] arrangements = [[0, 1, 2, 3], [0, 2, 1, 3], [0, 3, 1, 2]];
        var candidates = arrangements.Select(order =>
        {
            var first = Count(order[0], order[1], true);
            var second = Count(order[2], order[3], true);
            var opponents = new[] { Count(order[0], order[2], false), Count(order[0], order[3], false),
                Count(order[1], order[2], false), Count(order[1], order[3], false) };
            // Random persisted IDs provide a stable tie lottery for this court transition.
            // Re-reading/resetting a preview cannot change it or invalidate its own confirmation.
            var key = seed.ToString("N") + string.Concat(order.Select(i => selected[i].Id.ToString("N")));
            return new { Order = order, OverThreshold = (first.Total >= 2 ? 1 : 0) + (second.Total >= 2 ? 1 : 0),
                Cost = 4L * (first.Total + second.Total) + 8L * (first.Recent + second.Recent) + opponents.Sum(p => (long)p.Total + 2L * p.Recent), Tie = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) };
        }).ToArray();
        var belowThreshold = candidates.Where(candidate => candidate.OverThreshold == 0).ToArray();
        var ranked = (belowThreshold.Length > 0 ? belowThreshold : candidates)
            .OrderBy(candidate => candidate.Cost).ThenBy(candidate => candidate.Tie, StringComparer.Ordinal).First();
        return ranked.Order.Select(i => selected[i]).ToArray();
    }
}
