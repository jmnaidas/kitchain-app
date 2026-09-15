using Kitchain.Domain.Play;

namespace Kitchain.Tests.Play;

public sealed class PlaySessionFlowTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 0, 0, 0, TimeSpan.Zero);
    private static PlaySession Draft(int count = 8, int courts = 1, PlaySessionMode mode = PlaySessionMode.QueueOnly)
    {
        var session = new PlaySession(Guid.NewGuid(), "ABCDEF", "Crew", new(2026, 9, 10), new(18, 0), new(21, 0), courts, null, Now, mode);
        for (var i = 1; i <= count; i++) session.AddGuest($"Player {i}", Now);
        return session;
    }
    private static Guid[] Queue(PlaySession session) => session.WaitingQueue.Select(p => p.Id).ToArray();
    private static Guid[] Lineup(PlaySession session, PlayMatch match) => session.NextLineup(match.Id).Select(p => p.Id).ToArray();

    [Fact]
    public void Draft_rename_and_removal_preserve_FIFO_and_validate_names_and_lifecycle()
    {
        var session = Draft();
        var players = session.Players.ToArray();
        session.RenameGuest(players[0].Id, "  New name  ", Now);
        Assert.Equal("New name", players[0].DisplayName);
        Assert.Throws<PlayConflictException>(() => session.RenameGuest(players[1].Id, "NEW   NAME", Now));
        Assert.Throws<ArgumentException>(() => session.RenameGuest(players[0].Id, " ", Now));
        Assert.Throws<ArgumentException>(() => session.RenameGuest(players[0].Id, new string('x', 81), Now));
        Assert.Equal(players.Select(p => p.Id), Queue(session));
        session.RemoveGuest(players[1].Id, Now);
        Assert.Equal(players.Where(p => p != players[1]).Select(p => p.Id), Queue(session));
        Assert.Equal(new long?[] { 1, 3, 4, 5, 6, 7, 8 }, session.WaitingQueue.Select(p => p.QueueOrder));
        session.Start(Now);
        session.RenameGuest(players[0].Id, "Other", Now);
        Assert.Equal("Other", players[0].DisplayName);
        var playing = session.Players.First(p => p.State == PlayPlayerState.Playing);
        Assert.Throws<PlayConflictException>(() => session.RemoveGuest(playing.Id, Now));
    }

    [Fact]
    public void Queue_only_completion_and_preview_leave_players_and_tickets_untouched_until_confirmation()
    {
        var session = Draft(9);
        Assert.Equal(PlaySessionMode.QueueOnly, session.Mode);
        session.Start(Now);
        var match = Assert.Single(session.Matches);
        var queue = Queue(session);
        var tickets = session.NextQueueOrder;
        var returning = match.Players.OrderBy(p => p.Position).Select(p => p.PlayerId).ToArray();
        Assert.Throws<PlayConflictException>(() => session.RecordRally(match.Id, PlayTeam.A, Now));
        Assert.Throws<PlayConflictException>(() => session.CorrectScore(match.Id, 0, 0, PlayTeam.A, 2, Now));
        session.FinishGame(match.Id, Now);
        Assert.Equal(PlayMatchStatus.Completed, match.Status);
        Assert.Null(match.Winner);
        Assert.True(match.IsCurrent);
        Assert.Equal(queue.Take(4).Order(), Lineup(session, match).Order());
        Assert.Equal(queue.Take(4).Order(), Lineup(session, match).Order());
        Assert.Single(session.Matches);
        Assert.Equal(queue, Queue(session));
        Assert.Equal(tickets, session.NextQueueOrder);
        Assert.All(session.Players.Where(p => returning.Contains(p.Id)), p => Assert.Equal(PlayPlayerState.Playing, p.State));
        session.StartNextGame(match.Id, Lineup(session, match), false, Now);
        Assert.False(match.IsCurrent);
        var next = Assert.Single(session.Matches.Where(m => m.IsCurrent));
        Assert.Equal(queue.Take(4).Order(), next.Players.Select(p => p.PlayerId).Order());
        Assert.Equal(queue.Skip(4).Concat(returning), Queue(session));
        Assert.Equal(tickets + 4, session.NextQueueOrder);
        Assert.Throws<PlayConflictException>(() => session.StartNextGame(match.Id, queue.Take(4).ToArray(), false, Now));
    }

    [Fact]
    public void Explicit_override_preserves_slot_order_and_unselected_queue_order()
    {
        var session = Draft(10, mode: PlaySessionMode.LiveScoring);
        session.Start(Now);
        var match = Assert.Single(session.Matches);
        session.FinishGame(match.Id, Now);
        Assert.Null(match.Winner);
        var queue = Queue(session);
        var selected = new[] { queue[5], queue[1], queue[4], queue[0] };
        Assert.Throws<PlayConflictException>(() => session.StartNextGame(match.Id, selected, false, Now));
        session.StartNextGame(match.Id, selected, true, Now);
        var next = Assert.Single(session.Matches.Where(m => m.IsCurrent));
        Assert.Equal(selected, next.Players.OrderBy(p => p.Position).Select(p => p.PlayerId));
        Assert.Equal(new[] { PlayTeam.A, PlayTeam.A, PlayTeam.B, PlayTeam.B }, next.Players.OrderBy(p => p.Position).Select(p => p.Team));
        Assert.Equal(queue.Where(id => !selected.Contains(id)), Queue(session).Take(2));
        Assert.Equal((0, 0, PlayTeam.A, 2), (next.TeamAScore, next.TeamBScore, next.ServingTeam, next.CurrentServerNumber));
    }

    [Fact]
    public void Invalid_ineligible_and_stale_lineups_never_release_the_completed_court()
    {
        var session = Draft(14, 2);
        session.Start(Now);
        var match = session.Matches.First();
        var other = session.Matches.Last();
        session.FinishGame(match.Id, Now);
        var proposed = Lineup(session, match);
        Assert.Throws<ArgumentException>(() => session.StartNextGame(match.Id, proposed.Take(3).ToArray(), true, Now));
        Assert.Throws<ArgumentException>(() => session.StartNextGame(match.Id, new[] { proposed[0], proposed[0], proposed[1], proposed[2] }, true, Now));
        foreach (var invalid in new[] { Guid.NewGuid(), other.Players.First().PlayerId })
            Assert.Throws<PlayConflictException>(() => session.StartNextGame(match.Id, new[] { invalid, proposed[1], proposed[2], proposed[3] }, true, Now));
        session.Rest(proposed[0], Now);
        Assert.Throws<PlayConflictException>(() => session.StartNextGame(match.Id, proposed, true, Now));
        session.Rejoin(proposed[0], Now);
        Assert.Throws<PlayConflictException>(() => session.StartNextGame(match.Id, proposed, false, Now));
        Assert.True(match.IsCurrent);
        Assert.Equal(2, session.Matches.Count);
        Assert.Equal(PlayMatchStatus.Active, other.Status);
    }

    [Fact]
    public void Another_court_can_consume_a_proposal_but_cannot_create_duplicate_assignments()
    {
        var session = Draft(12, 2);
        session.Start(Now);
        var games = session.Matches.ToArray();
        foreach (var game in games) session.FinishGame(game.Id, Now);
        var stale = Lineup(session, games[0]);
        session.StartNextGame(games[1].Id, Lineup(session, games[1]), false, Now);
        Assert.Throws<PlayConflictException>(() => session.StartNextGame(games[0].Id, stale, true, Now));
        Assert.True(games[0].IsCurrent);
        var currentIds = session.Matches.Where(m => m.IsCurrent).SelectMany(m => m.Players).Select(p => p.PlayerId).ToArray();
        Assert.Equal(8, currentIds.Distinct().Count());
    }

    [Fact]
    public void Returning_players_complete_a_lineup_and_ended_sessions_preserve_final_state()
    {
        var session = Draft(7, mode: PlaySessionMode.LiveScoring);
        session.Start(Now);
        var match = Assert.Single(session.Matches);
        session.FinishGame(match.Id, Now);
        Assert.Equal(4, Lineup(session, match).Length);
        Assert.Throws<ArgumentException>(() => session.StartNextGame(match.Id, Lineup(session, match).Take(3).ToArray(), false, Now));
        session.End(Now);
        var queue = Queue(session);
        Assert.Equal(PlaySessionStatus.Ended, session.Status);
        Assert.True(match.IsCurrent);
        Assert.Equal(PlayMatchStatus.Completed, match.Status);
        Assert.Throws<PlayConflictException>(() => session.StartNextGame(match.Id, queue, true, Now));
        Assert.Throws<PlayConflictException>(() => session.RecordRally(match.Id, PlayTeam.A, Now));
        Assert.Throws<PlayConflictException>(() => session.CorrectScore(match.Id, 0, 0, PlayTeam.A, 2, Now));
        Assert.Throws<PlayConflictException>(() => session.AddGuest("Late", Now));
        Assert.Throws<PlayConflictException>(() => session.Rest(queue[0], Now));
        Assert.Throws<PlayConflictException>(() => session.Rejoin(queue[0], Now));
        Assert.Throws<PlayConflictException>(() => session.FinishGame(match.Id, Now));
        Assert.Throws<PlayConflictException>(() => session.RenameGuest(queue[0], "Late", Now));
        Assert.Throws<PlayConflictException>(() => session.RemoveGuest(queue[0], Now));
        Assert.Equal(queue, Queue(session));
        Assert.Single(session.Matches);
    }
}
