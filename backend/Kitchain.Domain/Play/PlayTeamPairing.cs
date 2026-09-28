using System.Security.Cryptography;
using System.Text;

namespace Kitchain.Domain.Play;

/// <summary>Arranges an already selected fair group; never selects different players.</summary>
internal static class PlayTeamPairing
{
    public static PlaySessionPlayer[] Recommend(Guid sessionId, IReadOnlyList<PlaySessionPlayer> selected,
        IEnumerable<PlayMatch> history, Guid seed, PlayMatch? avoidPartnersFrom = null) =>
        Rank(sessionId, selected, history, seed, avoidPartnersFrom, false);

    public static PlaySessionPlayer[] RecommendBalanced(Guid sessionId, IReadOnlyList<PlaySessionPlayer> selected,
        IEnumerable<PlayMatch> history, Guid seed) => Rank(sessionId, selected, history, seed, null, true);

    private static PlaySessionPlayer[] Rank(Guid sessionId, IReadOnlyList<PlaySessionPlayer> selected,
        IEnumerable<PlayMatch> history, Guid seed, PlayMatch? avoidPartnersFrom, bool balanced)
    {
        if (selected.Count != 4) return selected.ToArray();
        var matches = history.Where(m => m.SessionId == sessionId && m.Status == PlayMatchStatus.Completed).ToArray();
        int Count(int a, int b, bool teammates)
        {
            var first = selected[a].Id;
            var second = selected[b].Id;
            var meetings = matches.Where(m =>
            {
                var one = m.Players.SingleOrDefault(p => p.PlayerId == first);
                var two = m.Players.SingleOrDefault(p => p.PlayerId == second);
                return one is not null && two is not null && (one.Team == two.Team) == teammates;
            }).ToArray();
            return meetings.Length;
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
            bool PreviousPartners(int a, int b) => avoidPartnersFrom is not null &&
                avoidPartnersFrom.Players.Any(one => one.PlayerId == selected[a].Id &&
                    avoidPartnersFrom.Players.Any(two => two.PlayerId == selected[b].Id && two.Team == one.Team));
            return new { Order = order, ImmediateRepeats = (PreviousPartners(order[0], order[1]) ? 1 : 0) +
                    (PreviousPartners(order[2], order[3]) ? 1 : 0),
                Teammates = (long)first + second, Opponents = opponents.Sum(p => (long)p),
                KnownDifference = balanced ? PlayBalancedRotation.KnownCountDifference(order.Select(i => selected[i]).ToArray()) : 0,
                SkillDifference = balanced ? PlayBalancedRotation.StrengthDifference(order.Select(i => selected[i]).ToArray()) : 0,
                Tie = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) };
        }).ToArray();
        if (avoidPartnersFrom is not null)
            candidates = candidates.Where(candidate => candidate.ImmediateRepeats == candidates.Min(c => c.ImmediateRepeats)).ToArray();
        if (balanced)
        {
            // Preserve the exact opening behavior. With history, variety may trade at most
            // one observed skill level of team-average difference, never a severe mismatch.
            if (matches.Length == 0)
                return PlayBalancedRotation.Pair(Recommend(sessionId, selected, matches, seed));
            var known = candidates.Min(c => c.KnownDifference);
            candidates = candidates.Where(c => c.KnownDifference == known).ToArray();
            var best = candidates.Min(c => c.SkillDifference);
            candidates = candidates.Where(c => c.SkillDifference <= best + 1m).ToArray();
        }
        // Lifetime counts match the Matchups tab: one teammate and two opponents per
        // completed doubles match. Teammate repetition takes priority over opponents.
        var ranked = candidates.OrderBy(c => c.Teammates).ThenBy(c => c.Opponents)
            .ThenBy(c => c.SkillDifference).ThenBy(c => c.Tie, StringComparer.Ordinal).First();
        return ranked.Order.Select(i => selected[i]).ToArray();
    }
}
