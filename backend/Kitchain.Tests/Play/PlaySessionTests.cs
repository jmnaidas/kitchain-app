using Kitchain.Domain.Play;

namespace Kitchain.Tests.Play;

public sealed class PlaySessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 0, 0, 0, TimeSpan.Zero);
    private static PlaySession Session(int? maximumPlayers = null) => new(Guid.NewGuid(), "abcdef", " Evening play ",
        new DateOnly(2026, 9, 10), new TimeOnly(18, 0), new TimeOnly(21, 0), 2, maximumPlayers, Now);

    [Fact]
    public void Creation_sets_defaults_and_normalizes_code_and_name()
    {
        var session = Session();
        Assert.Equal("ABCDEF", session.JoinCode);
        Assert.Equal("Evening play", session.Name);
        Assert.Equal(PlaySessionStatus.Draft, session.Status);
        Assert.Equal(PlayRotationMode.FairRotation, session.RotationMode);
        Assert.Equal(PlayScoringMode.Traditional, session.ScoringMode);
        Assert.Equal(11, session.GameTo);
        Assert.Equal(2, session.WinBy);
        Assert.Equal(0, session.NextQueueOrder);
        Assert.Equal(Now, session.CreatedAt);
        Assert.Equal(session.CreatedAt, session.UpdatedAt);
        Assert.Empty(session.Players);
        Assert.Empty(session.WaitingQueue);
    }

    [Fact]
    public void Late_guests_and_rejoining_players_go_to_back_without_renumbering_others()
    {
        var session = Session();
        var alex = session.AddGuest(" Alex ", Now);
        var sam = session.AddGuest("Sam", Now);
        Assert.Equal("Alex", alex.DisplayName);
        Assert.Equal(PlayPlayerIdentityType.Guest, alex.IdentityType);
        Assert.Equal(PlayPlayerState.Waiting, alex.State);
        Assert.Equal(new[] { alex.Id, sam.Id }, session.WaitingQueue.Select(p => p.Id));
        session.Rest(alex.Id, Now.AddMinutes(1));
        Assert.Null(alex.QueueOrder);
        Assert.Equal(PlayPlayerState.Resting, alex.State);
        Assert.Equal(sam.Id, Assert.Single(session.WaitingQueue).Id);
        var lee = session.AddGuest("Lee", Now.AddMinutes(2));
        session.Rejoin(alex.Id, Now.AddMinutes(3));
        Assert.Equal(new[] { sam.Id, lee.Id, alex.Id }, session.WaitingQueue.Select(p => p.Id));
        Assert.Equal(new long?[] { 2, 3, 4 }, session.WaitingQueue.Select(p => p.QueueOrder));
        Assert.Equal(Now.AddMinutes(3), alex.UpdatedAt);
        Assert.Equal(4, session.NextQueueOrder);
        Assert.Throws<PlayConflictException>(() => session.Rejoin(alex.Id, Now.AddMinutes(4)));
        session.Rest(alex.Id, Now.AddMinutes(4));
        Assert.Throws<PlayConflictException>(() => session.Rest(alex.Id, Now.AddMinutes(5)));
        Assert.Throws<KeyNotFoundException>(() => session.Rest(Guid.NewGuid(), Now.AddMinutes(5)));
    }

    [Fact]
    public void Ambiguous_names_are_rejected_and_resting_players_still_count_toward_capacity()
    {
        var session = Session(2);
        var player = session.AddGuest("Alex  Cruz", Now);
        Assert.Throws<PlayConflictException>(() => session.AddGuest(" alex cruz ", Now));
        Assert.Equal(1, session.NextQueueOrder);
        session.Rest(player.Id, Now);
        Assert.Throws<PlayConflictException>(() => session.AddGuest("ALEX CRUZ", Now));
        session.AddGuest("Sam", Now);
        Assert.Throws<PlayConflictException>(() => session.AddGuest("Lee", Now));
        Assert.Equal(2, session.Players.Count);
        // Names are scoped to a session.
        Assert.Equal("Alex Cruz", Session().AddGuest("Alex Cruz", Now).DisplayName);
    }

    [Fact]
    public void Lifecycle_allows_start_once_and_ended_sessions_reject_queue_mutations()
    {
        var session = Session();
        Assert.Throws<PlayConflictException>(() => session.End(Now));
        var player = session.AddGuest("Alex", Now);
        session.Start(Now.AddMinutes(1));
        Assert.Equal(PlaySessionStatus.Active, session.Status);
        Assert.Throws<PlayConflictException>(() => session.Start(Now.AddMinutes(2)));
        session.Rest(player.Id, Now.AddMinutes(2));
        session.End(Now.AddMinutes(3));
        Assert.Equal(PlaySessionStatus.Ended, session.Status);
        Assert.Throws<PlayConflictException>(() => session.AddGuest("Sam", Now.AddMinutes(4)));
        Assert.Throws<PlayConflictException>(() => session.Rest(player.Id, Now.AddMinutes(4)));
        Assert.Throws<PlayConflictException>(() => session.Rejoin(player.Id, Now.AddMinutes(4)));
        Assert.Throws<PlayConflictException>(() => session.Start(Now.AddMinutes(4)));
        Assert.Throws<PlayConflictException>(() => session.End(Now.AddMinutes(4)));
    }

    [Fact]
    public void Invalid_schedule_capacity_code_and_names_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new PlaySession(Guid.NewGuid(), "ABCDEF", " ", new(2026, 9, 10), new(18, 0), new(21, 0), 1, null, Now));
        Assert.Throws<ArgumentException>(() => new PlaySession(Guid.NewGuid(), "ABCDEF", "Play", new(2026, 9, 10), new(21, 0), new(18, 0), 1, null, Now));
        Assert.Throws<ArgumentException>(() => new PlaySession(Guid.NewGuid(), "ABCDEF", "Play", default, new(18, 0), new(21, 0), 1, null, Now));
        Assert.Throws<ArgumentException>(() => new PlaySession(Guid.NewGuid(), "ABCDEF", "Play", new(2026, 9, 10), new(18, 0), new(21, 0), 0, null, Now));
        Assert.Throws<ArgumentException>(() => Session(0));
        Assert.Throws<ArgumentException>(() => new PlaySession(Guid.NewGuid(), "ABC01I", "Play", new(2026, 9, 10), new(18, 0), new(21, 0), 1, null, Now));
        Assert.Throws<ArgumentException>(() => Session().AddGuest(" ", Now));
        Assert.Throws<ArgumentException>(() => Session().AddGuest(new string('x', 81), Now));
    }
}
