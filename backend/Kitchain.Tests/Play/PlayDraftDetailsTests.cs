using Kitchain.Domain.Play;

namespace Kitchain.Tests.Play;

public sealed class PlayDraftDetailsTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 0, 0, 0, TimeSpan.Zero);
    private static PlaySession Draft() => new(Guid.NewGuid(), "ABCDEF", "Crew", new(2026, 9, 14), new(18, 0), new(21, 0), 1, null, Now);

    [Fact]
    public void Draft_break_and_rejoin_are_rejected_without_changing_roster()
    {
        var session = Draft();
        var player = session.AddGuest("Alex", Now);
        Assert.Throws<PlayConflictException>(() => session.Rest(player.Id, Now));
        Assert.Throws<PlayConflictException>(() => session.Rejoin(player.Id, Now));
        Assert.Equal(PlayPlayerState.Waiting, player.State);
        Assert.Equal(Now, player.WaitingSince);
        Assert.Equal(1, session.NextQueueOrder);
    }

    [Fact]
    public void Draft_edit_updates_all_structural_fields_and_preserves_roster()
    {
        var session = Draft();
        var player = session.AddGuest("Alex", Now);
        session.EditDetails(" Midnight crew ", new(2026, 9, 14), new(23, 0), new(2026, 9, 15), new(2, 0), 3, 12, PlaySessionMode.LiveScoring, Now.AddMinutes(1));
        Assert.Equal("Midnight crew", session.Name);
        Assert.Equal(new DateOnly(2026, 9, 15), session.EndDate);
        Assert.Equal(new TimeOnly(23, 0), session.StartTime);
        Assert.Equal(new TimeOnly(2, 0), session.EndTime);
        Assert.Equal(3, session.NumberOfCourts);
        Assert.Equal(12, session.MaximumPlayers);
        Assert.Equal(PlaySessionMode.LiveScoring, session.Mode);
        Assert.Same(player, Assert.Single(session.Players));
        Assert.Equal(1, player.QueueOrder);
        Assert.Equal(PlaySessionStatus.Draft, session.Status);
    }

    [Fact]
    public void Invalid_edits_are_atomic_and_active_or_ended_structure_is_read_only()
    {
        var session = Draft();
        session.AddGuest("Alex", Now);
        session.AddGuest("Blair", Now);
        Assert.Throws<PlayConflictException>(() => session.EditDetails("Changed", new(2026, 9, 14), new(18, 0), new(2026, 9, 14), new(21, 0), 4, 1, PlaySessionMode.LiveScoring, Now));
        Assert.Throws<ArgumentException>(() => session.EditDetails("Changed", new(2026, 9, 14), new(23, 0), new(2026, 9, 14), new(2, 0), 4, 12, PlaySessionMode.LiveScoring, Now));
        Assert.Throws<ArgumentException>(() => session.EditDetails(" ", new(2026, 9, 14), new(18, 0), new(2026, 9, 14), new(21, 0), 1, null, PlaySessionMode.QueueOnly, Now));
        Assert.Throws<ArgumentException>(() => session.EditDetails("Changed", new(2026, 9, 14), new(18, 0), new(2026, 9, 14), new(21, 0), 0, null, PlaySessionMode.QueueOnly, Now));
        Assert.Throws<ArgumentException>(() => session.EditDetails("Changed", new(2026, 9, 14), new(18, 0), new(2026, 9, 14), new(21, 0), 1, null, (PlaySessionMode)99, Now));
        Assert.Equal("Crew", session.Name);
        Assert.Equal(1, session.NumberOfCourts);
        Assert.Null(session.MaximumPlayers);
        Assert.Equal(PlaySessionMode.QueueOnly, session.Mode);
        session.Start(Now);

        session.StartReadyGames();
        Assert.Throws<PlayConflictException>(() => session.EditDetails("Changed", new(2026, 9, 14), new(18, 0), new(2026, 9, 14), new(21, 0), 1, null, PlaySessionMode.QueueOnly, Now));
        session.End(Now);
        Assert.Throws<PlayConflictException>(() => session.EditDetails("Changed", new(2026, 9, 14), new(18, 0), new(2026, 9, 14), new(21, 0), 1, null, PlaySessionMode.QueueOnly, Now));
    }

    [Theory]
    [InlineData(14, 18, 21, true)]
    [InlineData(15, 23, 2, true)]
    [InlineData(14, 23, 2, false)]
    [InlineData(13, 18, 21, false)]
    [InlineData(14, 18, 18, false)]
    public void Full_local_schedule_range_is_validated(int endDay, int startHour, int endHour, bool valid)
    {
        PlaySession Create() => new(Guid.NewGuid(), "ABCDEF", "Crew", new(2026, 9, 14), new(startHour, 0), new(endHour, 0), 1, null, Now, endDate: new(2026, 9, endDay));
        if (valid) Assert.Equal(new DateOnly(2026, 9, endDay), Create().EndDate);
        else Assert.Throws<ArgumentException>(() => Create());
        Assert.Equal(Draft().SessionDate, Draft().EndDate);
    }
}
