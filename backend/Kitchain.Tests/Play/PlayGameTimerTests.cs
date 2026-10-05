using Kitchain.Domain.Play;

namespace Kitchain.Tests.Play;

public sealed class PlayGameTimerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static PlaySession Session(PlaySessionMode mode)
    {
        var s = new PlaySession(Guid.NewGuid(), "ABCDEF", "Timers", new(2026, 10, 5),
            new(18, 0), new(22, 0), 2, null, Now, mode);
        for (var i = 0; i < 12; i++) s.AddGuest("Player " + i, Now);
        s.Start(Now);
        return s;
    }

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(null)]
    public void Timer_is_per_game_defaults_to_fifteen_and_locks_at_start(int? minutes)
    {
        var s = Session(PlaySessionMode.QueueOnly); var m = s.Matches.First(); var other = s.Matches.Last();
        Assert.All(s.Matches, game => Assert.Equal(15, game.TimerDurationMinutes));
        s.SetGameTimer(m.Id, minutes, m.LineupRevision, Now);
        Assert.Equal(minutes, m.TimerDurationMinutes); Assert.Equal(15, other.TimerDurationMinutes);
        Assert.Null(m.StartedAt); Assert.Equal(1, m.LineupRevision);
        Assert.Throws<PlayConflictException>(() => s.StartGame(m.Id, 0, Now));
        s.StartGame(m.Id, m.LineupRevision, Now);
        Assert.Equal(Now, m.StartedAt); Assert.Equal(minutes, m.TimerDurationMinutes);
        Assert.Throws<PlayConflictException>(() => s.SetGameTimer(m.Id, 20, m.LineupRevision, Now));
        Assert.Equal(PlayMatchStatus.Ready, other.Status);
    }

    [Theory]
    [InlineData(PlaySessionMode.QueueOnly)]
    [InlineData(PlaySessionMode.LiveScoring)]
    public void Overtime_does_not_change_lifecycle_and_host_can_score_finish_and_proceed(PlaySessionMode mode)
    {
        var s = Session(mode); var m = s.Matches.First();
        s.SetGameTimer(m.Id, 10, m.LineupRevision, Now);
        s.StartGame(m.Id, m.LineupRevision, Now);
        var later = Now.AddMinutes(25);
        s.AddGuest("Late", later); // An ordinary mutation after expiry cannot finish a game.
        Assert.Equal(PlayMatchStatus.Active, m.Status); Assert.Null(m.Winner); Assert.Null(m.CompletedAt);
        if (mode == PlaySessionMode.LiveScoring)
        {
            s.RecordRally(m.Id, PlayTeam.A, later);
            Assert.Equal(1, m.TeamAScore); Assert.Equal(PlayMatchStatus.Active, m.Status);
        }
        s.FinishGame(m.Id, later);
        s.StartNextGame(m.Id, s.NextLineup(m.Id).Select(p => p.Id).ToArray(), false, later);
        var ready = s.Matches.Single(game => game.IsCurrent && game.CourtNumber == m.CourtNumber);
        Assert.Equal(PlayMatchStatus.Ready, ready.Status); Assert.Equal(15, ready.TimerDurationMinutes);
        s.SetGameTimer(ready.Id, null, ready.LineupRevision, later);
        s.StartGame(ready.Id, ready.LineupRevision, later);
        s.FinishGame(ready.Id, later.AddHours(1));
        Assert.Null(ready.TimerDurationMinutes); Assert.Equal(later, ready.StartedAt);
    }

    [Fact]
    public void Invalid_stale_and_ended_edits_do_not_change_timer_or_revision()
    {
        var s = Session(PlaySessionMode.QueueOnly); var m = s.Matches.First();
        foreach (var invalid in new[] { -1, 0, 11, 30 })
            Assert.Throws<ArgumentException>(() => s.SetGameTimer(m.Id, invalid, 0, Now));
        Assert.Equal(15, m.TimerDurationMinutes); Assert.Equal(0, m.LineupRevision);
        s.SetGameTimer(m.Id, null, 0, Now);
        Assert.Throws<PlayConflictException>(() => s.SetGameTimer(m.Id, 10, 0, Now));
        s.End(Now);
        Assert.Throws<PlayConflictException>(() => s.SetGameTimer(m.Id, 10, 1, Now));
        Assert.Null(m.TimerDurationMinutes); Assert.Equal(1, m.LineupRevision);
    }
}
