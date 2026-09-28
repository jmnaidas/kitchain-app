using System.Text.Json;

namespace Kitchain.Domain.Play;

public sealed record PlayPlannedCourt(Guid MatchId, int CourtNumber, Guid[] PlayerIds, bool? Finalized = null);
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
        if (Status == PlaySessionStatus.Draft && NextRoundJson is not null)
        {
            NextRoundJson = JsonSerializer.Serialize(OpeningRound());
            return;
        }
        if (NextRoundJson is not null) SaveCourts(SavedActiveCourts());
    }

    public IReadOnlyList<PlaySessionPlayer> FutureEligiblePlayers()
    {
        if (Status == PlaySessionStatus.Ended) return [];
        var reserved = _matches.Where(m => m.IsCurrent && m.Status == PlayMatchStatus.Ready)
            .SelectMany(m => m.Players).Select(p => p.PlayerId).ToHashSet();
        return _players.Where(p => !p.IsRemoved && p.State != PlayPlayerState.Resting && !reserved.Contains(p.Id)).ToArray();
    }

    public PlayRoundPlan NextRound()
    {
        if (Status == PlaySessionStatus.Draft) return OpeningRound();
        if (Status != PlaySessionStatus.Active) return new(false, []);
        var saved = SavedActiveCourts();
        var assigned = saved.SelectMany(c => c.PlayerIds).ToHashSet();
        var eligible = FutureEligiblePlayers().Where(p => !assigned.Contains(p.Id)).ToArray();
        var anchors = _matches.Where(m => m.IsCurrent && m.Status != PlayMatchStatus.Ready && !saved.Any(c => c.MatchId == m.Id))
            .OrderBy(m => m.CourtNumber).Take(eligible.Length / 4).ToArray();
        var groups = RecommendRound(eligible, anchors.Select(m => m.Id).ToArray());
        var courts = saved.Concat(anchors.Select((m, i) => new PlayPlannedCourt(m.Id, m.CourtNumber,
            groups[i].Select(p => p.Id).ToArray(), false))).OrderBy(c => c.CourtNumber).ToArray();
        return new(courts.Length > 0 && courts.All(c => c.Finalized == true), courts);
    }

    private PlayPlannedCourt[] SavedActiveCourts()
    {
        if (Status != PlaySessionStatus.Active || NextRoundJson is null) return [];
        var saved = JsonSerializer.Deserialize<PlayRoundPlan>(NextRoundJson)!;
        var eligible = _players.Where(p => !p.IsRemoved && p.State != PlayPlayerState.Resting).Select(p => p.Id).ToHashSet();
        var used = new HashSet<Guid>();
        var anchors = new HashSet<Guid>();
        var valid = new List<PlayPlannedCourt>();
        foreach (var court in saved.Courts)
        {
            if (court.PlayerIds.Length != 4 || court.PlayerIds.Distinct().Count() != 4 ||
                court.PlayerIds.Any(id => !eligible.Contains(id) || used.Contains(id)) || anchors.Contains(court.MatchId) ||
                !_matches.Any(m => m.Id == court.MatchId && m.CourtNumber == court.CourtNumber && m.IsCurrent && m.Status != PlayMatchStatus.Ready)) continue;
            // Legacy JSON has one batch flag; preserve its intent when first read or written.
            valid.Add(court with { Finalized = court.Finalized ?? saved.Finalized });
            anchors.Add(court.MatchId);
            used.UnionWith(court.PlayerIds);
        }
        return valid.ToArray();
    }

    private void SaveCourts(PlayPlannedCourt[] courts) => NextRoundJson = courts.Length == 0 ? null :
        JsonSerializer.Serialize(new PlayRoundPlan(courts.All(c => c.Finalized == true), courts));

    public void EditNextRound(Guid matchId, int position, Guid playerId, long expectedRevision, DateTimeOffset now)
    {
        CheckPlan(expectedRevision, now);
        if (position is < 1 or > 4) throw new ArgumentException("Choose a position from 1 to 4.");
        var plan = NextRound();
        var court = plan.Courts.SingleOrDefault(c => c.MatchId == matchId)
            ?? throw new PlayConflictException("The next round changed. Refresh the session.");
        if (position > court.PlayerIds.Length)
            throw new PlayConflictException("That opening position is not assigned yet.");
        if (!FutureEligiblePlayers().Any(p => p.Id == playerId))
            throw new PlayConflictException("That player is unavailable for the next round.");
        var other = plan.Courts.SingleOrDefault(c => c.PlayerIds.Contains(playerId));
        if (other is not null)
            other.PlayerIds[Array.IndexOf(other.PlayerIds, playerId)] = court.PlayerIds[position - 1];
        court.PlayerIds[position - 1] = playerId;
        var changed = plan.Courts.Select(c => c.MatchId == matchId || c.MatchId == other?.MatchId
            ? c with { Finalized = false } : c).ToArray();
        SaveCourts(changed);
        Touch(now);
    }

    public void FinalizeNextRound(long expectedRevision, DateTimeOffset now, Guid? matchId = null)
    {
        CheckPlan(expectedRevision, now);
        if (Status != PlaySessionStatus.Active)
            throw new PlayConflictException("Start Session confirms the opening round.");
        var plan = NextRound();
        if (plan.Courts.Length == 0) throw new PlayConflictException("Start a game before planning the next round.");
        if (matchId.HasValue && !plan.Courts.Any(c => c.MatchId == matchId))
            throw new PlayConflictException("That court plan changed. Refresh the session.");
        SaveCourts(plan.Courts.Select(c => !matchId.HasValue || c.MatchId == matchId ? c with { Finalized = true } : c).ToArray());
        Touch(now);
    }

    public void ResetNextRound(long expectedRevision, DateTimeOffset now, Guid? matchId = null)
    {
        CheckPlan(expectedRevision, now);
        if (matchId.HasValue && Status == PlaySessionStatus.Active)
        {
            var plan = NextRound();
            if (!plan.Courts.Any(c => c.MatchId == matchId))
                throw new PlayConflictException("That court plan changed. Refresh the session.");
            SaveCourts(plan.Courts.Where(c => c.MatchId != matchId).ToArray());
        }
        else NextRoundJson = null;
        Touch(now);
    }

    private void CheckPlan(long expectedRevision, DateTimeOffset now)
    {
        EnsureOpen(now);
        if (expectedRevision != NextRoundRevision || NextRoundRevision == long.MaxValue)
            throw new PlayConflictException("The session or next round changed. Refresh before continuing.");
    }

    public void SetSkillLevel(Guid playerId, PlaySkillLevel? level, long expectedRevision, DateTimeOffset now)
    {
        CheckPlan(expectedRevision, now);
        FindPlayer(playerId).SetSkillLevel(level, now);
        Touch(now);
    }

    private PlaySessionPlayer[][] RecommendRound(IReadOnlyList<PlaySessionPlayer> eligible, Guid[] seeds)
    {
        var pool = eligible.ToList();
        var groups = seeds.Select(seed =>
        {
            var selected = Recommend(pool, seed).ToArray();
            pool.RemoveAll(selected.Contains);
            return selected;
        }).ToArray();
        return RotationMode == PlayRotationMode.BalancedRotation
            ? PlayBalancedRotation.Courts(groups, (i, players) => PlayTeamPairing.Recommend(Id, players, _matches, seeds[i]))
            : groups;
    }

    private Guid OpeningCourtId(int court) => new(System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes($"{Id:N}:opening:{court}"))[..16]);

    private PlayRoundPlan OpeningRound()
    {
        var eligible = FutureEligiblePlayers();
        var count = (int)Math.Min(NumberOfCourts, (eligible.Count + 3L) / 4);
        var saved = NextRoundJson is null ? null : JsonSerializer.Deserialize<PlayRoundPlan>(NextRoundJson);
        if (saved is null)
        {
            // Keep the existing opening Fair Rotation seed on every court.
            var groups = RecommendRound(eligible, Enumerable.Repeat(Id, count).ToArray());
            return new(false, groups.Select((g, i) => new PlayPlannedCourt(OpeningCourtId(i + 1), i + 1,
                g.Select(p => p.Id).ToArray())).ToArray());
        }

        // Keep valid manual slots, repairing only unavailable assignments and newly opened slots.
        var valid = eligible.Select(p => p.Id).ToHashSet();
        var used = new HashSet<Guid>();
        var slots = Enumerable.Range(1, count).Select(court =>
        {
            var previous = saved.Courts.SingleOrDefault(c => c.CourtNumber == court);
            return Enumerable.Range(0, 4).Select(i => previous is not null && i < previous.PlayerIds.Length &&
                valid.Contains(previous.PlayerIds[i]) && used.Add(previous.PlayerIds[i]) ? previous.PlayerIds[i] : Guid.Empty).ToArray();
        }).ToArray();
        var available = eligible.Where(p => !used.Contains(p.Id)).ToList();
        foreach (var court in slots)
        {
            var replacements = new Queue<Guid>(Recommend(available, Id).Select(p => p.Id));
            for (var i = 0; i < court.Length; i++)
            {
                if (court[i] != Guid.Empty || replacements.Count == 0) continue;
                court[i] = replacements.Dequeue();
                available.RemoveAll(p => p.Id == court[i]);
            }
        }
        return new(false, slots.Select((ids, i) => new PlayPlannedCourt(OpeningCourtId(i + 1), i + 1,
            ids.Where(id => id != Guid.Empty).ToArray())).Where(c => c.PlayerIds.Length > 0).ToArray());
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
                if (dependency?.Finalized != true || holder.Status != PlayMatchStatus.Completed) return [];
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
        SaveCourts(remaining);
        Touch(now);
    }
}
