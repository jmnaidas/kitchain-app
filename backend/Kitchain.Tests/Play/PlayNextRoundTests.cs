using Kitchain.Domain.Play;

namespace Kitchain.Tests.Play;

public sealed class PlayNextRoundTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static PlaySession Session(int players = 12, int courts = 2, PlayRotationMode rotation = PlayRotationMode.FairRotation)
    {
        var s = new PlaySession(Guid.NewGuid(), "ABCDEF", "Planner", new(2026, 9, 28), new(18, 0), new(22, 0), courts, null, Now,
            PlaySessionMode.QueueOnly, rotationMode: rotation);
        for (var i = 0; i < players; i++) s.AddGuest("Player " + i, Now);
        s.Start(Now); s.StartReadyGames();
        return s;
    }

    [Fact]
    public void Complete_multi_court_projection_edits_and_finalization_do_not_credit_participation()
    {
        var s = Session(20);
        var before = s.Players.ToDictionary(p => p.Id, p => (p.QueueOrder, p.WaitingSince, p.AdjustedGamesStarted, p.MissedOpportunities));
        var plan = s.NextRound(); var a = plan.Courts[0]; var b = plan.Courts[1];
        Assert.Equal(8, s.RotationPreview().Next.Count);
        Assert.Equal(8, plan.Courts.SelectMany(c => c.PlayerIds).Distinct().Count());
        s.EditNextRound(a.MatchId, 1, a.PlayerIds[2], s.NextRoundRevision, Now);
        Assert.Equal(a.PlayerIds[0], s.NextRound().Courts[0].PlayerIds[2]);
        var displaced = s.NextRound().Courts[0].PlayerIds[0];
        s.EditNextRound(a.MatchId, 1, b.PlayerIds[1], s.NextRoundRevision, Now);
        Assert.Equal(displaced, s.NextRound().Courts[1].PlayerIds[1]);
        var waiting = s.FutureEligiblePlayers().First(p => p.State == PlayPlayerState.Waiting && !s.NextRound().Courts.SelectMany(c => c.PlayerIds).Contains(p.Id));
        s.EditNextRound(a.MatchId, 2, waiting.Id, s.NextRoundRevision, Now);
        s.FinalizeNextRound(s.NextRoundRevision, Now);
        Assert.True(s.NextRound().Finalized);
        Assert.All(s.Players, p => Assert.Equal(before[p.Id], (p.QueueOrder, p.WaitingSince, p.AdjustedGamesStarted, p.MissedOpportunities)));
        Assert.All(s.Matches, m => Assert.Equal(PlayMatchStatus.Active, m.Status));
    }

    [Theory]
    [InlineData(PlayRotationMode.FairRotation)]
    [InlineData(PlayRotationMode.WinnersStay)]
    [InlineData(PlayRotationMode.ChallengersStay)]
    [InlineData(PlayRotationMode.SplitTeams)]
    public void Finalized_plan_promotes_to_ready_and_only_start_credits_final_players(PlayRotationMode rotation)
    {
        var s = Session(8, 1, rotation);
        var court = s.NextRound().Courts.Single();
        s.EditNextRound(court.MatchId, 2, court.PlayerIds[2], s.NextRoundRevision, Now);
        var final = s.NextRound().Courts.Single().PlayerIds;
        s.FinalizeNextRound(s.NextRoundRevision, Now);
        s.FinishGame(court.MatchId, Now, PlayTeam.A);
        Assert.Equal(final, s.NextLineup(court.MatchId).Select(p => p.Id));
        var counts = s.Players.ToDictionary(p => p.Id, p => p.AdjustedGamesStarted);
        s.StartNextGame(court.MatchId, final, false, Now);
        var ready = s.Matches.Single(m => m.IsCurrent);
        Assert.Equal(PlayMatchStatus.Ready, ready.Status);
        Assert.Equal(final, ready.Players.OrderBy(p => p.Position).Select(p => p.PlayerId));
        Assert.All(s.Players, p => Assert.Equal(counts[p.Id], p.AdjustedGamesStarted));
        s.StartGame(ready.Id, ready.LineupRevision, Now);
        Assert.All(s.Players.Where(p => final.Contains(p.Id)), p => Assert.Equal(counts[p.Id] + 1, p.AdjustedGamesStarted));
    }

    [Fact]
    public void Cross_court_future_players_wait_for_completion_then_promote_atomically()
    {
        var s = Session(8); var matches = s.Matches.OrderBy(m => m.CourtNumber).ToArray();
        // Explicitly propose each court's current four, then exchange their exact first slots.
        foreach (var m in matches)
            foreach (var slot in m.Players.OrderBy(p => p.Position))
                s.EditNextRound(m.Id, slot.Position, slot.PlayerId, s.NextRoundRevision, Now);
        s.EditNextRound(matches[0].Id, 1, matches[1].Players.First(p => p.Position == 1).PlayerId, s.NextRoundRevision, Now);
        var plan = s.NextRound();
        s.FinalizeNextRound(s.NextRoundRevision, Now);
        s.FinishGame(matches[0].Id, Now);
        Assert.Empty(s.NextLineup(matches[0].Id));
        Assert.Throws<PlayConflictException>(() => s.StartNextGame(matches[0].Id, plan.Courts[0].PlayerIds, false, Now));
        Assert.Equal(2, s.Matches.Count);
        s.FinishGame(matches[1].Id, Now);
        s.StartNextGame(matches[0].Id, plan.Courts[0].PlayerIds, false, Now);
        var ready = s.Matches.Where(m => m.IsCurrent).OrderBy(m => m.CourtNumber).ToArray();
        Assert.All(ready, m => Assert.Equal(PlayMatchStatus.Ready, m.Status));
        Assert.Equal(8, ready.SelectMany(m => m.Players).Select(p => p.PlayerId).Distinct().Count());
        for (var i = 0; i < 2; i++) Assert.Equal(plan.Courts[i].PlayerIds, ready[i].Players.OrderBy(p => p.Position).Select(p => p.PlayerId));
    }

    [Fact]
    public void Independent_court_consumes_only_its_plan_and_preserves_other_finalized_lineup()
    {
        var s = Session(20); var plan = s.NextRound();
        s.FinalizeNextRound(s.NextRoundRevision, Now);
        s.FinishGame(plan.Courts[0].MatchId, Now);
        s.StartNextGame(plan.Courts[0].MatchId, plan.Courts[0].PlayerIds, false, Now);
        Assert.Equal(PlayMatchStatus.Active, s.Matches.Single(m => m.Id == plan.Courts[1].MatchId).Status);
        Assert.True(s.NextRound().Finalized);
        Assert.Equal(plan.Courts[1].PlayerIds, Assert.Single(s.NextRound().Courts).PlayerIds);
    }

    [Fact]
    public void Roster_changes_invalidate_plan_and_stale_or_ended_mutations_are_rejected()
    {
        var s = Session(12); var plan = s.NextRound();
        s.FinalizeNextRound(s.NextRoundRevision, Now);
        var stale = s.NextRoundRevision;
        var id = plan.Courts.SelectMany(c => c.PlayerIds).First(id => s.Players.Single(p => p.Id == id).State == PlayPlayerState.Waiting);
        s.Rest(id, Now);
        Assert.False(s.NextRound().Finalized);
        Assert.DoesNotContain(id, s.NextRound().Courts.SelectMany(c => c.PlayerIds));
        Assert.Throws<PlayConflictException>(() => s.FinalizeNextRound(stale, Now));
        Assert.Throws<PlayConflictException>(() => s.EditNextRound(plan.Courts[0].MatchId, 1, id, s.NextRoundRevision, Now));
        Assert.NotNull(s.NextRoundJson);
        Assert.All(s.NextRound().Courts.Where(c => !plan.Courts.Single(p => p.MatchId == c.MatchId).PlayerIds.Contains(id)),
            c => Assert.True(c.Finalized));
        s.Rejoin(id, Now);
        Assert.False(s.NextRound().Finalized);
        s.End(Now);
        Assert.Empty(s.NextRound().Courts);
        Assert.Throws<PlayConflictException>(() => s.FinalizeNextRound(s.NextRoundRevision, Now));
    }
}

