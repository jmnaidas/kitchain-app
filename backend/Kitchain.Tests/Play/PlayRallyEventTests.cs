using Kitchain.Domain.Play;

namespace Kitchain.Tests.Play;

public sealed class PlayRallyEventTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 0, 0, 0, TimeSpan.Zero);
    private static PlaySession Session(PlaySessionMode mode = PlaySessionMode.LiveScoring)
    {
        var session = new PlaySession(Guid.NewGuid(), "ABCDEF", "Crew", new(2026, 9, 14), new(18, 0), new(21, 0), 2, null, Now, mode);
        for (var i = 0; i < 12; i++) session.AddGuest($"Player {i}", Now);
        session.Start(Now);

        session.StartReadyGames();
        return session;
    }
    private static PlayMatch Court(PlaySession session, int number = 1) => session.Matches.Single(m => m.IsCurrent && m.CourtNumber == number);
    private static object Snapshot(PlayRallyEvent rally) => (rally.Id, rally.MatchId, rally.Sequence, rally.Winner,
        rally.PointAwarded, rally.TeamAScore, rally.TeamBScore, rally.ServingTeam, rally.CurrentServerNumber, rally.CreatedAt);
    private static object[] Fairness(PlaySession session) => session.Players.OrderBy(p => p.Id)
        .Select(p => (object)(p.Id, p.State, p.QueueOrder, p.AdjustedGamesStarted, p.MissedOpportunities, p.WaitingSince)).ToArray();

    [Fact]
    public void Each_accepted_rally_records_its_winner_and_after_state_in_stable_order()
    {
        var session = Session();
        var match = Court(session);
        var fairness = Fairness(session);
        var winners = new[] { PlayTeam.A, PlayTeam.B, PlayTeam.B, PlayTeam.A, PlayTeam.A };
        var states = new[] { (1, 0, PlayTeam.A, 2, true), (1, 0, PlayTeam.B, 1, false),
            (1, 1, PlayTeam.B, 1, true), (1, 1, PlayTeam.B, 2, false), (1, 1, PlayTeam.A, 1, false) };
        for (var i = 0; i < winners.Length; i++)
        {
            session.RecordRally(match.Id, winners[i], Now.AddSeconds(i));
            var rally = match.Rallies.Single(r => r.Sequence == i + 1);
            Assert.Equal(states[i], (rally.TeamAScore, rally.TeamBScore, rally.ServingTeam, rally.CurrentServerNumber, rally.PointAwarded));
            Assert.Equal((match.TeamAScore, match.TeamBScore, match.ServingTeam, match.CurrentServerNumber),
                (rally.TeamAScore, rally.TeamBScore, rally.ServingTeam, rally.CurrentServerNumber));
            Assert.Equal(winners[i], rally.Winner);
            Assert.Equal(match.Id, rally.MatchId);
            Assert.Equal(Now.AddSeconds(i), rally.CreatedAt);
            Assert.Null(rally.CallOut);
        }
        Assert.Equal(5, match.Rallies.Select(r => r.Id).Distinct().Count());
        Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, match.Rallies.OrderBy(r => r.Sequence).Select(r => r.Sequence));
        Assert.Equal(fairness, Fairness(session));
        Assert.Empty(Court(session, 2).Rallies);
    }

    [Fact]
    public void Call_outs_can_be_added_changed_removed_but_never_rewrite_the_event_snapshot()
    {
        var session = Session();
        var match = Court(session);
        session.RecordRally(match.Id, PlayTeam.A, Now);
        var rally = Assert.Single(match.Rallies);
        var snapshot = Snapshot(rally);
        var fairness = Fairness(session);
        PlayRallyCallOut? previous = null;
        foreach (var tag in Enum.GetValues<PlayRallyCallOut>())
        {
            session.EditRallyCallOut(match.Id, rally.Id, tag, previous, Now);
            Assert.Equal(tag, rally.CallOut);
            Assert.Equal(snapshot, Snapshot(rally));
            previous = tag;
        }
        Assert.Throws<PlayConflictException>(() => session.EditRallyCallOut(match.Id, rally.Id, PlayRallyCallOut.Drive, null, Now));
        Assert.Throws<ArgumentException>(() => session.EditRallyCallOut(match.Id, rally.Id, (PlayRallyCallOut)99, previous, Now));
        Assert.Throws<KeyNotFoundException>(() => session.EditRallyCallOut(match.Id, Guid.NewGuid(), null, null, Now));
        Assert.Throws<KeyNotFoundException>(() => session.EditRallyCallOut(Court(session, 2).Id, rally.Id, null, previous, Now));
        Assert.Equal(previous, rally.CallOut);
        session.EditRallyCallOut(match.Id, rally.Id, null, previous, Now);
        Assert.Null(rally.CallOut);
        Assert.Equal(snapshot, Snapshot(rally));
        Assert.Equal(fairness, Fairness(session));
        Assert.Equal(1, match.TeamAScore);
        Assert.Single(match.Rallies);
    }

    [Fact]
    public void Corrections_preserve_recorded_snapshots_and_winning_rally_holds_history_until_confirmation()
    {
        var session = Session();
        var match = Court(session);
        session.RecordRally(match.Id, PlayTeam.B, Now);
        var first = Assert.Single(match.Rallies);
        var snapshot = Snapshot(first);
        var fairness = Fairness(session);
        session.CorrectScore(match.Id, 10, 9, PlayTeam.A, 2, Now);
        Assert.Single(match.Rallies);
        Assert.Equal(snapshot, Snapshot(first));
        session.RecordRally(match.Id, PlayTeam.A, Now);
        var winner = match.Rallies.Single(r => r.Sequence == 2);
        Assert.True(winner.PointAwarded);
        Assert.Equal(11, winner.TeamAScore);
        Assert.Equal(PlayMatchStatus.Completed, match.Status);
        Assert.True(match.IsCurrent);
        Assert.Equal(fairness, Fairness(session));
        session.EditRallyCallOut(match.Id, winner.Id, PlayRallyCallOut.Drive, null, Now);
        var proposal = session.NextLineup(match.Id).Select(p => p.Id).ToArray();
        session.EditRallyCallOut(match.Id, winner.Id, PlayRallyCallOut.Lob, PlayRallyCallOut.Drive, Now);
        Assert.Equal(proposal, session.NextLineup(match.Id).Select(p => p.Id));
        Assert.Throws<PlayConflictException>(() => session.RecordRally(match.Id, PlayTeam.A, Now));
        Assert.Equal(2, match.Rallies.Count);
        session.StartNextGame(match.Id, proposal, false, Now);

        session.StartReadyGames();
        Assert.Empty(Court(session).Rallies);
        Assert.Equal(2, match.Rallies.Count);
        Assert.Equal(PlayRallyCallOut.Lob, winner.CallOut);
        Assert.Throws<PlayConflictException>(() => session.EditRallyCallOut(match.Id, winner.Id, null, PlayRallyCallOut.Lob, Now));
        session.RecordRally(Court(session).Id, PlayTeam.B, Now);
        var fresh = Assert.Single(Court(session).Rallies);
        Assert.Equal(1, fresh.Sequence);
        Assert.Equal(Court(session).Id, fresh.MatchId);
        Assert.Equal(snapshot, Snapshot(first));
    }

    [Theory]
    [InlineData(PlaySessionMode.QueueOnly)]
    [InlineData(PlaySessionMode.LiveScoring)]
    public void Finish_override_never_invents_rallies_and_ended_sessions_are_read_only(PlaySessionMode mode)
    {
        var session = Session(mode);
        var match = Court(session);
        if (mode == PlaySessionMode.QueueOnly)
        {
            Assert.Throws<PlayConflictException>(() => session.RecordRally(match.Id, PlayTeam.A, Now));
            Assert.Throws<PlayConflictException>(() => session.EditRallyCallOut(match.Id, Guid.NewGuid(), null, null, Now));
        }
        session.FinishGame(match.Id, Now);
        Assert.Empty(match.Rallies);
        Assert.True(match.IsCurrent);
        Assert.Equal(4, session.NextLineup(match.Id).Count);
        session.End(Now);
        Assert.Throws<PlayConflictException>(() => session.EditRallyCallOut(match.Id, Guid.NewGuid(), null, null, Now));
        Assert.Empty(match.Rallies);
    }

    [Fact]
    public void Correction_completion_and_invalid_scoring_do_not_fabricate_or_delete_history()
    {
        var session = Session();
        var match = Court(session);
        Assert.Throws<ArgumentException>(() => session.RecordRally(match.Id, (PlayTeam)99, Now));
        Assert.Empty(match.Rallies);
        Assert.Equal(0, match.TeamAScore);
        session.RecordRally(match.Id, PlayTeam.A, Now);
        var recorded = Snapshot(Assert.Single(match.Rallies));
        session.CorrectScore(match.Id, 11, 9, PlayTeam.A, 2, Now);
        Assert.Equal(PlayMatchStatus.Completed, match.Status);
        Assert.Equal(recorded, Snapshot(Assert.Single(match.Rallies)));
        Assert.True(match.IsCurrent);
    }
}
