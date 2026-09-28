using System.Text.Json;

namespace Kitchain.Domain.Play;

public sealed record PlayPlannedCourt(Guid MatchId, int CourtNumber, Guid[] PlayerIds);
public sealed record PlayRoundPlan(bool Finalized, PlayPlannedCourt[] Courts);

public sealed partial class PlaySession
{
    // Future assignments are deliberately separate from match/participation history.
    public string? NextRoundJson { get; private set; }
    public long NextRoundRevision { get; private set; }

    private void Touch(DateTimeOffset now)
    {
        NextRoundRevision = checked(NextRoundRevision + 1);
        UpdatedAt = now.ToUniversalTime();
        if (NextRoundJson is not null && !ValidPlan(JsonSerializer.Deserialize<PlayRoundPlan>(NextRoundJson)!, FutureEligiblePlayers()))
            NextRoundJson = null;
    }

    public IReadOnlyList<PlaySessionPlayer> FutureEligiblePlayers()
    {
        if (Status != PlaySessionStatus.Active) return [];
        var reserved = _matches.Where(m => m.IsCurrent && m.Status == PlayMatchStatus.Ready)
            .SelectMany(m => m.Players).Select(p => p.PlayerId).ToHashSet();
        return _players.Where(p => !p.IsRemoved && p.State != PlayPlayerState.Resting && !reserved.Contains(p.Id)).ToArray();
    }

    public PlayRoundPlan NextRound()
    {
        if (Status != PlaySessionStatus.Active) return new(false, []);
        var saved = NextRoundJson is null ? null : JsonSerializer.Deserialize<PlayRoundPlan>(NextRoundJson);
        var eligible = FutureEligiblePlayers();
        if (saved is not null && ValidPlan(saved, eligible))
            return saved;

        var pool = eligible.ToList();
        var courts = new List<PlayPlannedCourt>();
        foreach (var match in _matches.Where(m => m.IsCurrent && m.Status != PlayMatchStatus.Ready).OrderBy(m => m.CourtNumber))
        {
            if (pool.Count < 4) break;
            var selected = Recommend(pool, match.Id).Select(p => p.Id).ToArray();
            courts.Add(new(match.Id, match.CourtNumber, selected));
            pool.RemoveAll(p => selected.Contains(p.Id));
        }
        return new(false, courts.ToArray());
    }

    private bool ValidPlan(PlayRoundPlan plan, IReadOnlyList<PlaySessionPlayer> eligible)
    {
        var ids = eligible.Select(p => p.Id).ToHashSet();
        return Status == PlaySessionStatus.Active && plan.Courts.Length > 0 &&
            plan.Courts.Select(c => c.MatchId).Distinct().Count() == plan.Courts.Length &&
            plan.Courts.All(c => c.PlayerIds.Length == 4 && _matches.Any(m => m.Id == c.MatchId &&
                m.CourtNumber == c.CourtNumber && m.IsCurrent && m.Status != PlayMatchStatus.Ready)) &&
            plan.Courts.SelectMany(c => c.PlayerIds).All(ids.Contains) &&
            plan.Courts.SelectMany(c => c.PlayerIds).Distinct().Count() == plan.Courts.Length * 4;
    }

    public void EditNextRound(Guid matchId, int position, Guid playerId, long expectedRevision, DateTimeOffset now)
    {
        CheckPlan(expectedRevision, now);
        if (position is < 1 or > 4) throw new ArgumentException("Choose a position from 1 to 4.");
        var plan = NextRound();
        var court = plan.Courts.SingleOrDefault(c => c.MatchId == matchId)
            ?? throw new PlayConflictException("The next round changed. Refresh the session.");
        if (!FutureEligiblePlayers().Any(p => p.Id == playerId))
            throw new PlayConflictException("That player is unavailable for the next round.");
        var other = plan.Courts.SingleOrDefault(c => c.PlayerIds.Contains(playerId));
        if (other is not null)
            other.PlayerIds[Array.IndexOf(other.PlayerIds, playerId)] = court.PlayerIds[position - 1];
        court.PlayerIds[position - 1] = playerId;
        NextRoundJson = JsonSerializer.Serialize(plan with { Finalized = false });
        Touch(now);
    }

    public void FinalizeNextRound(long expectedRevision, DateTimeOffset now)
    {
        CheckPlan(expectedRevision, now);
        var plan = NextRound();
        if (plan.Courts.Length == 0) throw new PlayConflictException("Start a game before planning the next round.");
        NextRoundJson = JsonSerializer.Serialize(plan with { Finalized = true });
        Touch(now);
    }

    public void ResetNextRound(long expectedRevision, DateTimeOffset now)
    {
        CheckPlan(expectedRevision, now);
        NextRoundJson = null;
        Touch(now);
    }

    private void CheckPlan(long expectedRevision, DateTimeOffset now)
    {
        EnsureOpen(now);
        if (Status != PlaySessionStatus.Active || expectedRevision != NextRoundRevision || NextRoundRevision == long.MaxValue)
            throw new PlayConflictException("The session or next round changed. Refresh before continuing.");
    }

    // A cross-court future assignment can depend on several completed holds. Promote only
    // that connected group together, leaving unrelated courts free to progress independently.
    private PlayPlannedCourt[] PlanDependencies(PlayRoundPlan plan, Guid matchId)
    {
        var result = new List<PlayPlannedCourt> { plan.Courts.Single(c => c.MatchId == matchId) };
        for (var i = 0; i < result.Count; i++)
        {
            var current = _matches.Single(m => m.Id == result[i].MatchId);
            if (!current.IsCurrent || current.Status != PlayMatchStatus.Completed) return [];
            foreach (var id in result[i].PlayerIds)
            {
                var holder = _matches.SingleOrDefault(m => m.IsCurrent && m.Players.Any(p => p.PlayerId == id));
                if (holder is null || result.Any(c => c.MatchId == holder.Id)) continue;
                var dependency = plan.Courts.SingleOrDefault(c => c.MatchId == holder.Id);
                if (dependency is null || holder.Status != PlayMatchStatus.Completed) return [];
                result.Add(dependency);
            }
        }
        return result.ToArray();
    }

    private void PromotePlan(PlayRoundPlan plan, Guid matchId, IReadOnlyList<Guid> requested, DateTimeOffset now)
    {
        var courts = PlanDependencies(plan, matchId);
        if (courts.Length == 0)
            throw new PlayConflictException("The finalized lineup is waiting for players on another court to finish.");
        if (!requested.SequenceEqual(courts[0].PlayerIds))
            throw new PlayConflictException("The finalized lineup changed. Refresh before proceeding.");
        var matches = courts.Select(c => _matches.Single(m => m.Id == c.MatchId)).ToArray();
        var returning = matches.SelectMany(m => m.Players.OrderBy(p => p.Position)).Select(p => FindPlayer(p.PlayerId)).ToArray();
        if (returning.Any(p => p.State != PlayPlayerState.Playing) || NextQueueOrder > long.MaxValue - returning.Length)
            throw new PlayConflictException("The returning players or queue changed. Refresh the session.");
        foreach (var player in returning) player.Finish(++NextQueueOrder, now);
        foreach (var court in courts)
        {
            _matches.Single(m => m.Id == court.MatchId).ReleaseCourt();
            var selected = court.PlayerIds.Select(FindPlayer).ToArray();
            var ready = new PlayMatch(Id, court.CourtNumber, selected, now);
            ready.SetLineup(selected, true);
            _matches.Add(ready);
        }
        var remaining = plan.Courts.Where(c => !courts.Contains(c)).ToArray();
        NextRoundJson = remaining.Length == 0 ? null : JsonSerializer.Serialize(plan with { Courts = remaining });
        Touch(now);
    }
}
