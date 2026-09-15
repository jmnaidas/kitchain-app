using Kitchain.Domain.Play;

namespace Kitchain.Tests.Play;

public sealed class PlayFairRotationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 0, 0, 0, TimeSpan.Zero);
    private static PlaySession Session(int count, int courts = 2)
    {
        var session = new PlaySession(Guid.NewGuid(), "ABCDEF", "Crew", new(2026, 9, 14), new(18, 0), new(21, 0), courts, null, Now);
        for (var i = 0; i < count; i++) session.AddGuest($"Player {i}", Now);
        session.Start(Now);
        return session;
    }
    private static PlayMatch Court(PlaySession session, int court = 1) => session.Matches.Single(m => m.IsCurrent && m.CourtNumber == court);
    private static Guid[] Ids(PlayMatch match) => match.Players.OrderBy(p => p.Position).Select(p => p.PlayerId).ToArray();
    private static Guid[] Preview(PlaySession session, PlayMatch match) => session.NextLineup(match.Id).Select(p => p.Id).ToArray();
    private static void Continue(PlaySession session, Guid[]? manual = null, int court = 1)
    {
        var match = Court(session, court);
        var now = session.UpdatedAt.AddMinutes(10);
        session.FinishGame(match.Id, now);
        session.StartNextGame(match.Id, manual ?? Preview(session, match), manual is not null, now);
    }
    private static object[] Stats(PlaySession session) => session.Players.OrderBy(p => p.Id)
        .Select(p => (object)(p.Id, p.State, p.AdjustedGamesStarted, p.MissedOpportunities, p.WaitingSince, p.QueueOrder)).ToArray();

    [Theory]
    [InlineData(8, 0)]
    [InlineData(9, 1)]
    [InlineData(10, 2)]
    [InlineData(11, 3)]
    [InlineData(12, 4)]
    [InlineData(16, 4)]
    public void First_games_take_priority_and_returning_players_complete_odd_or_small_pools(int count, int waitingSelected)
    {
        var session = Session(count);
        var match = Court(session);
        var other = Court(session, 2);
        var otherIds = Ids(other);
        var waiting = session.WaitingQueue.Select(p => p.Id).ToHashSet();
        var returning = Ids(match);
        session.FinishGame(match.Id, Now);
        var proposal = Preview(session, match);
        Assert.Equal(4, proposal.Distinct().Count());
        Assert.Equal(waitingSelected, proposal.Count(waiting.Contains));
        Assert.Equal(4 - waitingSelected, proposal.Count(returning.Contains));
        Assert.All(otherIds, id => Assert.DoesNotContain(id, proposal));
        session.StartNextGame(match.Id, proposal, false, Now);
        Assert.Same(other, Court(session, 2));
        Assert.Equal(otherIds, Ids(other));
        Assert.Equal(8, session.Matches.Where(m => m.IsCurrent).SelectMany(Ids).Distinct().Count());
    }

    [Fact]
    public void Equal_opportunities_mix_previous_quartets_and_reduce_shared_court_repeats()
    {
        var session = Session(12);
        var firstGroup = Ids(Court(session));
        var secondGroup = Ids(Court(session, 2));
        var unserved = session.WaitingQueue.Select(p => p.Id).ToArray();
        Continue(session);
        Assert.Equal(unserved.Order(), Ids(Court(session)).Order());
        var second = Court(session, 2);
        session.FinishGame(second.Id, session.UpdatedAt);
        var proposal = Preview(session, second);
        // Both eligible groups have one start and sit-out band zero. Two from each
        // minimizes shared-court repeats instead of rebuilding either old quartet.
        Assert.Equal(2, proposal.Count(firstGroup.Contains));
        Assert.Equal(2, proposal.Count(secondGroup.Contains));
        Assert.DoesNotContain(proposal, unserved.Contains);
        Assert.Equal(proposal, Preview(session, second));
    }

    [Fact]
    public void Two_missed_starts_protect_a_tier_even_when_its_exact_quartet_repeats()
    {
        var session = Session(12, 1);
        var first = Ids(Court(session));
        var waiting = session.WaitingQueue.Select(p => p.Id).ToArray();
        Continue(session, waiting.Take(4).ToArray());
        Continue(session, waiting.Skip(4).ToArray());
        var current = Court(session);
        session.FinishGame(current.Id, session.UpdatedAt);
        Assert.All(session.Players, p => Assert.Equal(1, p.AdjustedGamesStarted));
        Assert.All(session.Players.Where(p => first.Contains(p.Id)), p => Assert.Equal(2, p.MissedOpportunities));
        Assert.Equal(first.Order(), Preview(session, current).Order());
    }

    [Fact]
    public void Preview_and_completion_are_pure_and_confirmation_counts_exact_manual_starts()
    {
        var session = Session(10);
        var match = Court(session);
        var otherIds = Ids(Court(session, 2));
        var before = Stats(session);
        var history = session.Matches.Count;
        session.FinishGame(match.Id, Now);
        var proposal = Preview(session, match);
        for (var i = 0; i < 5; i++) Assert.Equal(proposal, Preview(session, match));
        Assert.Equal(before, Stats(session));
        Assert.Equal(history, session.Matches.Count);
        var selected = Ids(match).Reverse().ToArray();
        var eligible = session.EligibleNextPlayers(match.Id).ToArray();
        var counts = eligible.ToDictionary(p => p.Id, p => (p.AdjustedGamesStarted, p.MissedOpportunities));
        session.StartNextGame(match.Id, selected, true, Now);
        Assert.Equal(selected, Ids(Court(session)));
        foreach (var player in eligible)
        {
            Assert.Equal(counts[player.Id].AdjustedGamesStarted + (selected.Contains(player.Id) ? 1 : 0), player.AdjustedGamesStarted);
            Assert.Equal(selected.Contains(player.Id) ? 0 : counts[player.Id].MissedOpportunities + 1, player.MissedOpportunities);
        }
        Assert.All(session.Players.Where(p => otherIds.Contains(p.Id)), p => Assert.Equal(1, p.AdjustedGamesStarted));
        Assert.Equal(history + 1, session.Matches.Count);
    }

    [Fact]
    public void Stale_overlapping_previews_fail_without_moving_players_or_mutating_stats()
    {
        var session = Session(12);
        var first = Court(session);
        var second = Court(session, 2);
        session.FinishGame(first.Id, Now);
        session.FinishGame(second.Id, Now);
        var stale = Preview(session, first);
        Assert.All(Ids(second), id => Assert.DoesNotContain(id, session.EligibleNextPlayers(first.Id).Select(p => p.Id)));
        session.StartNextGame(second.Id, Preview(session, second), false, Now);
        var stats = Stats(session);
        var ticket = session.NextQueueOrder;
        Assert.Throws<PlayConflictException>(() => session.StartNextGame(first.Id, stale, true, Now));
        Assert.Equal(stats, Stats(session));
        Assert.Equal(ticket, session.NextQueueOrder);
        Assert.True(first.IsCurrent);
    }

    [Fact]
    public void Late_arrival_uses_lower_median_and_rest_does_not_earn_credit()
    {
        var session = Session(12);
        var resting = session.WaitingQueue[0];
        Assert.Equal(2, resting.MissedOpportunities);
        session.Rest(resting.Id, Now);
        Assert.Null(resting.WaitingSince);
        Assert.Equal(0, resting.MissedOpportunities);
        Continue(session, Ids(Court(session)));
        var current = Court(session);
        session.FinishGame(current.Id, session.UpdatedAt);
        Assert.DoesNotContain(resting, session.EligibleNextPlayers(current.Id));
        var expected = session.Players.Where(p => p.State != PlayPlayerState.Resting)
            .Select(p => p.AdjustedGamesStarted).Order().ToArray();
        var baseline = expected[(expected.Length - 1) / 2];
        Assert.True(baseline > 0);
        var joinedAt = session.UpdatedAt.AddMinutes(1);
        var late = session.AddGuest("Late", joinedAt);
        Assert.Equal(baseline, late.AdjustedGamesStarted);
        Assert.Equal(0, late.MissedOpportunities);
        Assert.Equal(joinedAt, late.WaitingSince);
        Assert.DoesNotContain(session.Matches.SelectMany(m => m.Players), p => p.PlayerId == late.Id);
        session.Rejoin(resting.Id, joinedAt.AddMinutes(1));
        Assert.Equal(baseline, resting.AdjustedGamesStarted);
        Assert.Equal(0, resting.MissedOpportunities);
        Assert.Equal(joinedAt.AddMinutes(1), resting.WaitingSince);
        Assert.Contains(resting, session.EligibleNextPlayers(current.Id));
    }
}
