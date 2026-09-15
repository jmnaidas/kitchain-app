using System.Security.Cryptography;
using System.Text;

namespace Kitchain.Domain.Play;

/// <summary>Protects opportunity tiers before optimizing a bounded set of equally fair groups.</summary>
internal static class PlayFairRotation
{
    public static PlaySessionPlayer[] Select(IReadOnlyList<PlaySessionPlayer> eligible,
        IEnumerable<PlayMatch> history, Guid seed)
    {
        if (eligible.Count < 4) return eligible.ToArray();
        var matches = history.OrderByDescending(m => m.StartedAt).ThenByDescending(m => m.Id).ToArray();
        var mandatory = new List<PlaySessionPlayer>();
        foreach (var tier in eligible.GroupBy(p => (p.AdjustedGamesStarted, Band: p.MissedOpportunities / 2))
            .OrderBy(g => g.Key.AdjustedGamesStarted).ThenByDescending(g => g.Key.Band))
        {
            var needed = 4 - mandatory.Count;
            if (tier.Count() <= needed)
            {
                mandatory.AddRange(tier);
                if (mandatory.Count == 4) return mandatory.OrderBy(p => p.Id).ToArray();
                continue;
            }
            // Only the boundary tier is capped; higher-priority players are never dropped.
            var pool = tier.OrderBy(p => p.WaitingSince ?? DateTimeOffset.MaxValue)
                .ThenBy(p => p.QueueOrder ?? long.MaxValue).ThenBy(p => Tie(seed, [p.Id]), StringComparer.Ordinal).Take(12).ToArray();
            var recent = matches.SelectMany(m => m.Players.Select(p => (p.PlayerId, Match: m)))
                .GroupBy(x => x.PlayerId).ToDictionary(g => g.Key, g => g.Take(3).Select(x => x.Match.Id).ToHashSet());
            var choices = Combinations(pool, needed).Select(extra =>
            {
                var group = mandatory.Concat(extra).OrderBy(p => p.Id).ToArray();
                var ids = group.Select(p => p.Id).ToHashSet();
                var relevant = matches.Where(m => group.Any(p => recent.GetValueOrDefault(p.Id)?.Contains(m.Id) == true)).ToArray();
                var quartetRepeats = relevant.Count(m => m.Players.Count == 4 && m.Players.All(p => ids.Contains(p.PlayerId)));
                var shared = relevant.Sum(m => { var n = m.Players.Count(p => ids.Contains(p.PlayerId)); return n * (n - 1) / 2; });
                return new { Group = group, QuartetRepeats = quartetRepeats, Shared = shared,
                    Continuing = group.Count(p => p.State == PlayPlayerState.Playing),
                    Wait = group.Sum(p => (decimal)(p.WaitingSince ?? DateTimeOffset.MaxValue).UtcTicks),
                    Ticket = group.Sum(p => (decimal)(p.QueueOrder ?? long.MaxValue)), Tie = Tie(seed, ids) };
            });
            return choices.OrderBy(x => x.QuartetRepeats).ThenBy(x => x.Shared).ThenBy(x => x.Continuing)
                .ThenBy(x => x.Wait).ThenBy(x => x.Ticket).ThenBy(x => x.Tie, StringComparer.Ordinal).First().Group;
        }
        return mandatory.ToArray();
    }

    internal static string Tie(Guid seed, IEnumerable<Guid> ids) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(seed.ToString("N") + string.Concat(ids.Order().Select(id => id.ToString("N"))))));

    private static IEnumerable<PlaySessionPlayer[]> Combinations(PlaySessionPlayer[] pool, int count, int start = 0)
    {
        if (count == 0) { yield return []; yield break; }
        for (var i = start; i <= pool.Length - count; i++)
            foreach (var tail in Combinations(pool, count - 1, i + 1))
                yield return new[] { pool[i] }.Concat(tail).ToArray();
    }
}
