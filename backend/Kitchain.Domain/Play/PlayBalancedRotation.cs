namespace Kitchain.Domain.Play;

/// <summary>Rearranges only players already selected fairly; unknown skill is never assigned a value.</summary>
internal static class PlayBalancedRotation
{
    public static PlaySessionPlayer[] Pair(IReadOnlyList<PlaySessionPlayer> fair)
    {
        if (fair.Count != 4 || fair.Count(p => p.SkillLevel.HasValue) < 2) return fair.ToArray();
        var arrangements = new[] { new[] { 0, 1, 2, 3 }, new[] { 0, 2, 1, 3 }, new[] { 0, 3, 1, 2 } };
        // Stable ordering preserves the fair/history-aware pairing when skill costs tie.
        return arrangements.Select(order => order.Select(i => fair[i]).ToArray())
            .OrderBy(KnownCountDifference).ThenBy(StrengthDifference).First();
    }

    internal static int KnownCountDifference(PlaySessionPlayer[] players) =>
        Math.Abs(players.Take(2).Count(p => p.SkillLevel.HasValue) - players.Skip(2).Count(p => p.SkillLevel.HasValue));

    internal static decimal StrengthDifference(PlaySessionPlayer[] players)
    {
        var a = players.Take(2).Where(p => p.SkillLevel.HasValue).Select(p => (decimal)p.SkillLevel!.Value).ToArray();
        var b = players.Skip(2).Where(p => p.SkillLevel.HasValue).Select(p => (decimal)p.SkillLevel!.Value).ToArray();
        // Compare only observed levels, with equal known counts preferred above.
        return a.Length == 0 || b.Length == 0 ? 0 : Math.Abs(a.Average() - b.Average());
    }

    public static PlaySessionPlayer[][] Courts(PlaySessionPlayer[][] fair,
        Func<int, IReadOnlyList<PlaySessionPlayer>, IReadOnlyList<PlaySessionPlayer>> pair)
    {
        var complete = fair.TakeWhile(c => c.Length == 4).ToArray();
        if (complete.Length < 2 || complete.SelectMany(c => c).Any(p => !p.SkillLevel.HasValue))
            return fair.Select(Pair).ToArray();

        // Four serpentine passes distribute known skill across the fairly selected courts.
        // Incomplete courts keep their original players; incomplete information never reshuffles courts.
        var ordered = complete.SelectMany(c => c).OrderByDescending(p => p.SkillLevel).ThenBy(p => p.Id).ToArray();
        var groups = complete.Select(_ => new List<PlaySessionPlayer>()).ToArray();
        for (var i = 0; i < ordered.Length; i++)
        {
            var column = i % groups.Length;
            var court = i / groups.Length % 2 == 0 ? column : groups.Length - 1 - column;
            groups[court].Add(ordered[i]);
        }
        // Do not discard the fair/history-aware court grouping for an equally good skill spread.
        static decimal Spread(IEnumerable<IEnumerable<PlaySessionPlayer>> courts)
        {
            var strengths = courts.Select(c => c.Sum(p => (decimal)p.SkillLevel!.Value)).ToArray();
            var average = strengths.Average();
            return strengths.Sum(total => (total - average) * (total - average));
        }
        if (Spread(groups) >= Spread(complete)) return fair.Select(Pair).ToArray();
        return groups.Select((players, i) => Pair(pair(i, players)))
            .Concat(fair.Skip(complete.Length)).ToArray();
    }
}
