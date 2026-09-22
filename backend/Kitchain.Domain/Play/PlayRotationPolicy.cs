namespace Kitchain.Domain.Play;

/// <summary>Applies court-retention preferences to the existing eligible pool and fair selector.</summary>
internal static class PlayRotationPolicy
{
    public static IReadOnlyList<PlaySessionPlayer> Recommend(Guid sessionId, PlayRotationMode mode,
        IReadOnlyList<PlaySessionPlayer> eligible, IReadOnlyCollection<PlayMatch> history, Guid seed)
    {
        var previous = history.SingleOrDefault(m => m.Id == seed && m.Status == PlayMatchStatus.Completed);
        PlaySessionPlayer[] Fair(IReadOnlyList<PlaySessionPlayer> pool, int count) =>
            PlayFairRotation.Select(pool, history, seed, count);
        IReadOnlyList<PlaySessionPlayer> Pair(IReadOnlyList<PlaySessionPlayer> selected) =>
            PlayTeamPairing.Recommend(sessionId, selected, history, seed);

        // Initial games, no-result completions and Fair Rotation keep the original path.
        if (mode == PlayRotationMode.FairRotation || previous?.Winner is null || eligible.Count < 4)
            return Pair(Fair(eligible, 4));

        var safe = eligible.Where(p => !p.IsRemoved && p.State != PlayPlayerState.Resting).ToArray();
        PlaySessionPlayer[] Team(PlayTeam team) => safe.Where(p => previous.Players
            .Any(slot => slot.PlayerId == p.Id && slot.Team == team)).ToArray();
        var winners = Team(previous.Winner.Value);
        var losers = Team(previous.Winner == PlayTeam.A ? PlayTeam.B : PlayTeam.A);
        // Never retain an unavailable player to satisfy a preset.
        if (winners.Length != 2 || losers.Length != 2) return Pair(Fair(safe, 4));

        var retained = mode switch
        {
            PlayRotationMode.WinnersStay => winners.OrderBy(p => p.Id).ToArray(),
            PlayRotationMode.ChallengersStay => losers.OrderBy(p => p.Id).ToArray(),
            PlayRotationMode.SplitTeams => Fair(winners, 1).Concat(Fair(losers, 1)).ToArray(),
            _ => []
        };
        if (retained.Length != 2) return Pair(Fair(safe, 4));
        var previousIds = previous.Players.Select(p => p.PlayerId).ToHashSet();
        var incoming = Fair(safe.Where(p => p.State == PlayPlayerState.Waiting && !previousIds.Contains(p.Id)).ToArray(), 2);
        var selected = retained.Concat(incoming).ToArray();
        // Small rosters can reuse returning players; never fabricate a slot or borrow another court's hold.
        if (selected.Length < 4)
            selected = selected.Concat(Fair(safe.Where(p => !selected.Contains(p)).ToArray(), 4 - selected.Length)).ToArray();

        // Pair-stay presets intentionally preserve the retained partnership. Split Teams uses the
        // existing variety ranking, which can mix the retained players with their incoming partners.
        return mode == PlayRotationMode.SplitTeams
            ? PlayTeamPairing.Recommend(sessionId, selected, history, seed, previous) : selected;
    }
}
