using Kitchain.Domain.Play;

namespace Kitchain.Tests.Play;

public sealed class PlayRotationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 0, 0, 0, TimeSpan.Zero);
    private static PlaySession Session(int courts) => new(Guid.NewGuid(), "ABCDEF", "Crew",
        new(2026, 9, 10), new(18, 0), new(21, 0), courts, null, Now);
    private static PlaySessionPlayer[] Add(PlaySession session, int count) => Enumerable.Range(1, count)
        .Select(i => session.AddGuest($"Player {i}", Now)).ToArray();
    private static Guid[] Participants(PlayMatch match) => match.Players.OrderBy(p => p.Position).Select(p => p.PlayerId).ToArray();

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Start_fills_lowest_courts_with_complete_FIFO_groups_and_deterministic_teams(int courts)
    {
        var session = Session(courts);
        var players = Add(session, 14);
        session.Start(Now);
        var matches = session.Matches.OrderBy(m => m.CourtNumber).ToArray();
        Assert.Equal(courts, matches.Length);
        for (var i = 0; i < courts; i++)
        {
            Assert.Equal(i + 1, matches[i].CourtNumber);
            Assert.Equal(players.Skip(i * 4).Take(4).Select(p => p.Id), Participants(matches[i]));
            Assert.Equal(new[] { PlayTeam.A, PlayTeam.A, PlayTeam.B, PlayTeam.B }, matches[i].Players.OrderBy(p => p.Position).Select(p => p.Team));
        }
        Assert.Equal(players.Skip(courts * 4).Select(p => p.Id), session.WaitingQueue.Select(p => p.Id));
        Assert.All(players.Take(courts * 4), p => { Assert.Equal(PlayPlayerState.Playing, p.State); Assert.Null(p.QueueOrder); });
    }

    [Fact]
    public void Finishing_court_two_preserves_court_one_and_requeues_completed_players_at_back()
    {
        var session = Session(2);
        var players = Add(session, 14);
        session.Start(Now);
        var first = session.Matches.Single(m => m.CourtNumber == 1);
        var second = session.Matches.Single(m => m.CourtNumber == 2);
        session.FinishGame(second.Id, Now.AddMinutes(10));
        Assert.Equal(PlayMatchStatus.Completed, second.Status);
        Assert.Equal(Now.AddMinutes(10), second.CompletedAt);
        Assert.Equal(PlayMatchStatus.Active, first.Status);
        Assert.Equal(players.Take(4).Select(p => p.Id), Participants(first));
        var replacement = session.Matches.Single(m => m.CourtNumber == 2 && m.Status == PlayMatchStatus.Active);
        Assert.Equal(players.Skip(8).Take(4).Select(p => p.Id), Participants(replacement));
        Assert.Equal(players.Skip(12).Concat(players.Skip(4).Take(4)).Select(p => p.Id), session.WaitingQueue.Select(p => p.Id));
        Assert.Equal(new long?[] { 13, 14, 15, 16, 17, 18 }, session.WaitingQueue.Select(p => p.QueueOrder));
        Assert.Throws<PlayConflictException>(() => session.FinishGame(second.Id, Now.AddMinutes(11)));
        Assert.Equal(2, session.Matches.Count(m => m.Status == PlayMatchStatus.Active));
    }

    [Fact]
    public void Insufficient_players_wait_until_a_late_guest_completes_four()
    {
        var session = Session(2);
        var players = Add(session, 3);
        session.Start(Now);
        Assert.Empty(session.Matches);
        Assert.Equal(3, session.WaitingQueue.Count);
        var late = session.AddGuest("Late guest", Now);
        var match = Assert.Single(session.Matches);
        Assert.Equal(1, match.CourtNumber);
        Assert.Equal(players.Append(late).Select(p => p.Id), Participants(match));
        Assert.Empty(session.WaitingQueue);
    }

    [Fact]
    public void Resting_is_excluded_rejoin_fills_free_court_and_playing_cannot_break()
    {
        var session = Session(1);
        var players = Add(session, 4);
        session.Rest(players[0].Id, Now);
        session.Start(Now);
        Assert.Empty(session.Matches);
        session.Rejoin(players[0].Id, Now);
        var match = Assert.Single(session.Matches);
        Assert.Equal(players.Skip(1).Append(players[0]).Select(p => p.Id), Participants(match));
        Assert.Throws<PlayConflictException>(() => session.Rest(players[0].Id, Now));
        Assert.Equal(PlayPlayerState.Playing, players[0].State);
    }

    [Fact]
    public void Repeated_rotation_never_duplicates_active_participants_or_courts()
    {
        var session = Session(2);
        Add(session, 9);
        session.Start(Now);
        for (var turn = 0; turn < 6; turn++)
        {
            var match = session.Matches.First(m => m.Status == PlayMatchStatus.Active);
            session.FinishGame(match.Id, Now);
            var active = session.Matches.Where(m => m.Status == PlayMatchStatus.Active).ToArray();
            var ids = active.SelectMany(Participants).ToArray();
            Assert.Equal(2, active.Select(m => m.CourtNumber).Distinct().Count());
            Assert.Equal(8, ids.Distinct().Count());
            Assert.DoesNotContain(session.WaitingQueue, p => ids.Contains(p.Id));
            Assert.Single(session.WaitingQueue);
        }
    }
}
