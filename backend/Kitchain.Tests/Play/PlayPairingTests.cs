using Kitchain.Domain.Play;

namespace Kitchain.Tests.Play;

public sealed class PlayPairingTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 0, 0, 0, TimeSpan.Zero);
    private static PlaySession Session(int count = 8, int courts = 1, PlaySessionMode mode = PlaySessionMode.QueueOnly)
    {
        var session = new PlaySession(Guid.NewGuid(), "ABCDEF", "Crew", new(2026, 9, 14), new(18, 0), new(21, 0), courts, null, Now, mode);
        for (var i = 0; i < count; i++) session.AddGuest($"Player {i}", session.UpdatedAt);
        session.Start(Now);

        session.StartReadyGames();
        return session;
    }
    private static PlayMatch Current(PlaySession session, int court = 1) => session.Matches.Single(m => m.IsCurrent && m.CourtNumber == court);
    private static Guid[] Slots(PlayMatch match) => match.Players.OrderBy(p => p.Position).Select(p => p.PlayerId).ToArray();
    private static Guid[] Proposal(PlaySession session) => session.NextLineup(Current(session).Id).Select(p => p.Id).ToArray();
    private static void Continue(PlaySession session, Guid[] slots)
    {
        var match = Current(session);
        session.FinishGame(match.Id, session.UpdatedAt.AddMinutes(1));
        session.StartNextGame(match.Id, slots, true, session.UpdatedAt);

        session.StartReadyGames();
        Assert.Equal(slots, Slots(Current(session)));
    }
    private static bool Partners(Guid[] slots, Guid a, Guid b) =>
        Array.IndexOf(slots, a) / 2 == Array.IndexOf(slots, b) / 2;
    private static int Count(PlaySession session, Guid a, Guid b) => session.Matches.Count(m =>
        m.Players.Any(p => p.PlayerId == a) && m.Players.Any(p => p.PlayerId == b) && Partners(Slots(m), a, b));
    private static void RestrictProposalToOriginalFour(PlaySession session)
    {
        foreach (var player in session.WaitingQueue.ToArray()) session.Rest(player.Id, session.UpdatedAt);
        session.FinishGame(Current(session).Id, session.UpdatedAt);
    }

    [Theory]
    [InlineData(PlaySessionMode.QueueOnly)]
    [InlineData(PlaySessionMode.LiveScoring)]
    public void Opportunity_priority_precedes_pairing_and_preview_has_no_effect(PlaySessionMode mode)
    {
        var session = Session(14, 2, mode);
        var court = Current(session);
        var other = Current(session, 2);
        var otherSlots = Slots(other);
        var queue = session.WaitingQueue.Select(p => p.Id).ToArray();
        var ticket = session.NextQueueOrder;
        session.FinishGame(court.Id, session.UpdatedAt);
        var recommendation = Proposal(session);
        Assert.Equal(queue.Take(4).Order(), recommendation.Order());
        Assert.Equal(recommendation, Proposal(session));
        Assert.Equal(2, session.Matches.Count);
        Assert.Equal(ticket, session.NextQueueOrder);
        Assert.Equal(queue, session.WaitingQueue.Select(p => p.Id));
        session.StartNextGame(court.Id, recommendation, false, session.UpdatedAt);

        session.StartReadyGames();
        Assert.Equal(recommendation, Slots(Current(session)));
        Assert.Same(other, Current(session, 2));
        Assert.Equal(otherSlots, Slots(other));
        Assert.Equal((0, 0, PlayTeam.A, 2), (other.TeamAScore, other.TeamBScore, other.ServingTeam, other.CurrentServerNumber));
    }

    [Fact]
    public void Twice_used_pairs_are_avoided_and_only_actual_session_matches_count()
    {
        var session = Session();
        var original = Slots(Current(session));
        Assert.Equal(1, Count(session, original[0], original[1]));
        Continue(session, original);
        Assert.Equal(2, Count(session, original[0], original[1]));
        RestrictProposalToOriginalFour(session);
        var proposal = Proposal(session);
        Assert.Equal(original.Order(), proposal.Order());
        Assert.False(Partners(proposal, original[0], original[1]));
        Assert.False(Partners(proposal, original[2], original[3]));
        var before = session.Matches.Count;
        for (var i = 0; i < 5; i++) Assert.Equal(proposal, Proposal(session));
        Assert.Equal(before, session.Matches.Count);
        Assert.Equal(0, Count(session, proposal[0], proposal[1]));
        var unrelated = Session();
        Continue(unrelated, Slots(Current(unrelated)));
        Assert.Equal(proposal, Proposal(session));
        Assert.All(unrelated.Matches, match => Assert.Equal(unrelated.Id, match.SessionId));
        session.StartNextGame(Current(session).Id, proposal, false, session.UpdatedAt);

        session.StartReadyGames();
        Assert.Equal(1, Count(session, proposal[0], proposal[1]));
    }

    [Fact]
    public void Unavoidable_repeats_use_lowest_cost_without_blocking_and_host_can_repeat_any_pair()
    {
        var session = Session();
        var p = Slots(Current(session));
        var arrangements = new[] { p, new[] { p[0], p[2], p[1], p[3] }, new[] { p[0], p[3], p[1], p[2] } };
        // Final historical counts per pair: 3, 2 and 4 respectively.
        Continue(session, arrangements[0]);
        Continue(session, arrangements[0]);
        for (var i = 0; i < 2; i++) Continue(session, arrangements[1]);
        for (var i = 0; i < 4; i++) Continue(session, arrangements[2]);
        RestrictProposalToOriginalFour(session);
        var proposal = Proposal(session);
        Assert.True(Partners(proposal, p[0], p[2]));
        Assert.True(Partners(proposal, p[1], p[3]));
        session.StartNextGame(Current(session).Id, proposal, false, session.UpdatedAt);

        session.StartReadyGames();
        Continue(session, arrangements[2]);
        Assert.Equal(arrangements[2], Slots(Current(session)));
        Assert.Equal(5, Count(session, p[0], p[3]));
    }

    [Fact]
    public void A_once_used_pair_remains_recommended_when_alternatives_have_reached_the_threshold()
    {
        var session = Session();
        var p = Slots(Current(session));
        var alternatives = new[] { new[] { p[0], p[2], p[1], p[3] }, new[] { p[0], p[3], p[1], p[2] } };
        foreach (var slots in alternatives)
            for (var i = 0; i < 2; i++) Continue(session, slots);
        RestrictProposalToOriginalFour(session);
        var recommendation = Proposal(session);
        Assert.True(Partners(recommendation, p[0], p[1]));
        Assert.True(Partners(recommendation, p[2], p[3]));
        session.StartNextGame(Current(session).Id, recommendation, false, session.UpdatedAt);

        session.StartReadyGames();
        Assert.Equal(2, Count(session, p[0], p[1]));
    }

    [Fact]
    public void Continuing_completed_players_is_an_override_and_preserves_unselected_FIFO_and_other_court()
    {
        var session = Session(14, 2, PlaySessionMode.LiveScoring);
        var court = Current(session);
        var other = Current(session, 2);
        session.CorrectScore(other.Id, 6, 5, PlayTeam.B, 1, session.UpdatedAt);
        var old = Slots(court);
        var waiting = session.WaitingQueue.Select(p => p.Id).ToArray();
        session.Rest(waiting[5], session.UpdatedAt);
        session.FinishGame(court.Id, session.UpdatedAt);
        var eligible = session.EligibleNextPlayers(court.Id).Select(p => p.Id).ToArray();
        Assert.All(old, id => Assert.Contains(id, eligible));
        Assert.DoesNotContain(waiting[5], eligible);
        Assert.All(Slots(other), id => Assert.DoesNotContain(id, eligible));
        var selected = new[] { old[0], waiting[0], waiting[1], waiting[3] };
        Assert.Throws<PlayConflictException>(() => session.StartNextGame(court.Id, selected, false, Now));
        var tickets = session.NextQueueOrder;
        Assert.Throws<PlayConflictException>(() => session.StartNextGame(court.Id, new[] { old[0], waiting[0], waiting[1], Slots(other)[0] }, true, Now));
        Assert.Equal(tickets, session.NextQueueOrder);
        Assert.True(court.IsCurrent);
        session.StartNextGame(court.Id, selected, true, session.UpdatedAt);

        session.StartReadyGames();
        Assert.Equal(selected, Slots(Current(session)));
        Assert.Equal(new[] { waiting[2], waiting[4] }.Concat(old.Skip(1)), session.WaitingQueue.Select(p => p.Id));
        Assert.Equal((6, 5, PlayTeam.B, 1), (other.TeamAScore, other.TeamBScore, other.ServingTeam, other.CurrentServerNumber));
        var next = Current(session);
        Assert.Equal((0, 0, PlayTeam.A, 2), (next.TeamAScore, next.TeamBScore, next.ServingTeam, next.CurrentServerNumber));
    }

    [Fact]
    public void Four_players_can_continue_automatically_or_manually_without_waiting_players()
    {
        var session = Session(4);
        var court = Current(session);
        var selected = Slots(court);
        session.FinishGame(court.Id, session.UpdatedAt);
        Assert.Equal(selected.Order(), Proposal(session).Order());
        Assert.Equal(4, session.EligibleNextPlayers(court.Id).Count);
        session.StartNextGame(court.Id, selected, true, session.UpdatedAt);

        session.StartReadyGames();
        Assert.Equal(selected, Slots(Current(session)));
        Assert.Empty(session.WaitingQueue);
        Assert.Equal(2, session.Matches.Count);
    }

    [Theory]
    [InlineData(11, 9, PlayTeam.A)]
    [InlineData(9, 11, PlayTeam.B)]
    [InlineData(15, 13, PlayTeam.A)]
    public void Winning_correction_holds_result_players_queue_and_other_court(int a, int b, PlayTeam winner)
    {
        var session = Session(12, 2, PlaySessionMode.LiveScoring);
        var court = Current(session);
        var other = Current(session, 2);
        var queue = session.WaitingQueue.Select(p => p.Id).ToArray();
        session.CorrectScore(other.Id, 4, 6, PlayTeam.B, 2, session.UpdatedAt);
        session.CorrectScore(court.Id, a, b, PlayTeam.A, 1, session.UpdatedAt);
        Assert.Equal(PlayMatchStatus.Completed, court.Status);
        Assert.Equal(winner, court.Winner);
        Assert.True(court.IsCurrent);
        Assert.Equal(2, session.Matches.Count);
        Assert.Equal(queue, session.WaitingQueue.Select(p => p.Id));
        Assert.All(session.Players.Where(p => Slots(court).Contains(p.Id)), p => Assert.Equal(PlayPlayerState.Playing, p.State));
        Assert.Equal((4, 6, PlayTeam.B, 2), (other.TeamAScore, other.TeamBScore, other.ServingTeam, other.CurrentServerNumber));
        session.StartNextGame(court.Id, Proposal(session), false, session.UpdatedAt);

        session.StartReadyGames();
        Assert.Equal(3, session.Matches.Count);
        Assert.Same(other, Current(session, 2));
    }
}
