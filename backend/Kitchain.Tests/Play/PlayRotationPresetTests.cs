using Kitchain.Domain.Play;

namespace Kitchain.Tests.Play;

public sealed class PlayRotationPresetTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 0, 0, 0, TimeSpan.Zero);
    private static PlaySession Session(PlayRotationMode mode = PlayRotationMode.FairRotation, int count = 8, int courts = 1)
    {
        var session = new PlaySession(Guid.NewGuid(), "ABCDEF", "Crew", new(2026, 9, 16), new(18, 0), new(21, 0),
            courts, null, Now, rotationMode: mode);
        for (var i = 0; i < count; i++) session.AddGuest($"Player {i}", Now);
        return session;
    }
    private static PlayMatch Court(PlaySession session, int court = 1) => session.Matches.Single(m => m.IsCurrent && m.CourtNumber == court);
    private static Guid[] Ids(PlayMatch match) => match.Players.OrderBy(p => p.Position).Select(p => p.PlayerId).ToArray();
    private static Guid[] Next(PlaySession session, PlayMatch match) => session.NextLineup(match.Id).Select(p => p.Id).ToArray();
    private static void Edit(PlaySession session, PlayRotationMode? mode) => session.EditDetails("Crew", new(2026, 9, 16),
        new(18, 0), new(2026, 9, 16), new(21, 0), 1, null, PlaySessionMode.QueueOnly, Now, mode);

    [Fact]
    public void Default_and_draft_configuration_preserve_legacy_edits_and_lock_after_start()
    {
        var session = Session();
        Assert.Equal(PlayRotationMode.FairRotation, session.RotationMode);
        foreach (var mode in Enum.GetValues<PlayRotationMode>())
        {
            Edit(session, mode);
            Assert.Equal(mode, session.RotationMode);
        }
        Edit(session, null);
        Assert.Equal(PlayRotationMode.SplitTeams, session.RotationMode);
        Assert.Throws<ArgumentException>(() => Edit(session, (PlayRotationMode)99));
        Assert.Equal(PlayRotationMode.SplitTeams, session.RotationMode);
        session.Start(Now);
        session.StartReadyGames();
        Assert.Throws<PlayConflictException>(() => Edit(session, PlayRotationMode.FairRotation));
        session.End(Now);
        Assert.Throws<PlayConflictException>(() => Edit(session, PlayRotationMode.FairRotation));
        Assert.Empty(session.RotationPreview().Next);
    }

    [Theory]
    [InlineData(PlayRotationMode.WinnersStay, PlayTeam.A)]
    [InlineData(PlayRotationMode.WinnersStay, PlayTeam.B)]
    [InlineData(PlayRotationMode.ChallengersStay, PlayTeam.A)]
    [InlineData(PlayRotationMode.ChallengersStay, PlayTeam.B)]
    public void Pair_stays_together_and_incoming_players_get_fair_opportunities(PlayRotationMode mode, PlayTeam winner)
    {
        var session = Session(mode, 10);
        session.Start(Now);
        session.StartReadyGames();
        var match = Court(session);
        var retainedTeam = mode == PlayRotationMode.WinnersStay ? winner : winner == PlayTeam.A ? PlayTeam.B : PlayTeam.A;
        var retained = match.Players.Where(p => p.Team == retainedTeam).Select(p => p.PlayerId).ToArray();
        var rotated = Ids(match).Except(retained).ToArray();
        session.FinishGame(match.Id, Now, winner);
        var next = Next(session, match);
        Assert.Equal(retained.Order(), next.Take(2).Order());
        Assert.All(next.Skip(2), id => Assert.Contains(session.WaitingQueue, p => p.Id == id && p.AdjustedGamesStarted == 0));
        Assert.Equal(next, Next(session, match));
        Assert.Equal(next.Skip(2).Order(), session.RotationPreview().Next.Select(p => p.Id).Order());
        Assert.Equal(2, session.RotationPreview().Held);
        var history = Ids(match);
        session.StartNextGame(match.Id, next, false, Now);
        session.StartReadyGames();
        Assert.All(rotated, id => Assert.Contains(session.WaitingQueue, p => p.Id == id));
        Assert.Equal(history, Ids(match));
        Assert.Equal(winner, match.Winner);
        var second = Court(session);
        session.FinishGame(second.Id, Now, mode == PlayRotationMode.WinnersStay ? PlayTeam.A : PlayTeam.B);
        var secondNext = Next(session, second);
        Assert.All(secondNext.Skip(2), id => Assert.Contains(session.WaitingQueue, p => p.Id == id && p.AdjustedGamesStarted == 0));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(8)]
    public void Split_teams_retains_one_from_each_pair_when_possible_and_never_recreates_partners(int count)
    {
        var session = Session(PlayRotationMode.SplitTeams, count);
        session.Start(Now);
        session.StartReadyGames();
        var match = Court(session);
        session.FinishGame(match.Id, Now, PlayTeam.B);
        var next = Next(session, match);
        Assert.Equal(next, Next(session, match));
        Assert.Equal(4, next.Distinct().Count());
        if (count >= 6)
        {
            Assert.Single(match.Players, p => p.Team == PlayTeam.A && next.Contains(p.PlayerId));
            Assert.Single(match.Players, p => p.Team == PlayTeam.B && next.Contains(p.PlayerId));
        }
        for (var i = 0; i < 4; i += 2)
        {
            var previous = match.Players.Where(p => next.Skip(i).Take(2).Contains(p.PlayerId)).ToArray();
            Assert.False(previous.Length == 2 && previous[0].Team == previous[1].Team);
        }
        session.StartNextGame(match.Id, next, false, Now);
        session.StartReadyGames();
        Assert.Equal(next, Ids(Court(session)));
    }

    [Theory]
    [InlineData(PlayRotationMode.FairRotation)]
    [InlineData(PlayRotationMode.WinnersStay)]
    [InlineData(PlayRotationMode.ChallengersStay)]
    [InlineData(PlayRotationMode.SplitTeams)]
    public void No_result_uses_fair_rotation_with_all_four_waiting_players(PlayRotationMode mode)
    {
        var session = Session(mode);
        session.Start(Now);
        session.StartReadyGames();
        var match = Court(session);
        var waiting = session.WaitingQueue.Select(p => p.Id).ToArray();
        session.FinishGame(match.Id, Now);
        Assert.Null(match.Winner);
        Assert.Equal(waiting.Order(), Next(session, match).Order());
        Assert.Equal(0, session.RotationPreview().Held);
    }

    [Theory]
    [InlineData(PlayRotationMode.WinnersStay)]
    [InlineData(PlayRotationMode.ChallengersStay)]
    [InlineData(PlayRotationMode.SplitTeams)]
    public void Live_scoring_uses_recorded_winner_and_does_not_infer_one_from_an_unfinished_score(PlayRotationMode mode)
    {
        var session = Session(mode);
        session.EditDetails("Crew", new(2026, 9, 16), new(18, 0), new(2026, 9, 16), new(21, 0),
            1, null, PlaySessionMode.LiveScoring, Now);
        session.Start(Now);
        session.StartReadyGames();
        var first = Court(session);
        var waiting = session.WaitingQueue.Select(p => p.Id).ToArray();
        session.CorrectScore(first.Id, 8, 2, PlayTeam.A, 1, Now);
        session.FinishGame(first.Id, Now);
        Assert.Null(first.Winner);
        Assert.Equal(waiting.Order(), Next(session, first).Order());
        session.StartNextGame(first.Id, Next(session, first), false, Now);
        session.StartReadyGames();
        var second = Court(session);
        session.CorrectScore(second.Id, 10, 8, PlayTeam.A, 1, Now);
        session.RecordRally(second.Id, PlayTeam.A, Now);
        Assert.Equal(PlayTeam.A, second.Winner);
        var next = Next(session, second);
        var retainedTeam = mode == PlayRotationMode.WinnersStay ? PlayTeam.A : PlayTeam.B;
        Assert.Equal(mode == PlayRotationMode.SplitTeams ? 1 : 2,
            second.Players.Count(p => p.Team == retainedTeam && next.Contains(p.PlayerId)));
        Assert.Single(second.Rallies);
        Assert.Equal(11, second.TeamAScore);
    }

    [Theory]
    [InlineData(PlayRotationMode.WinnersStay)]
    [InlineData(PlayRotationMode.ChallengersStay)]
    [InlineData(PlayRotationMode.SplitTeams)]
    public void Eligibility_overrides_preference_and_held_players_cannot_sit_out_or_be_removed(PlayRotationMode mode)
    {
        var session = Session(mode);
        session.Start(Now);
        session.StartReadyGames();
        var match = Court(session);
        var resting = session.WaitingQueue[0];
        var removed = session.WaitingQueue[1];
        session.Rest(resting.Id, Now);
        session.RemoveGuest(removed.Id, Now);
        session.FinishGame(match.Id, Now, PlayTeam.A);
        Assert.Throws<PlayConflictException>(() => session.Rest(Ids(match)[0], Now));
        Assert.Throws<PlayConflictException>(() => session.RemoveGuest(Ids(match)[0], Now));
        var next = Next(session, match);
        Assert.DoesNotContain(resting.Id, next);
        Assert.DoesNotContain(removed.Id, next);
        Assert.Equal(4, next.Distinct().Count());
        session.StartNextGame(match.Id, next, false, Now);
        session.StartReadyGames();
    }

    [Theory]
    [InlineData(PlayRotationMode.WinnersStay)]
    [InlineData(PlayRotationMode.ChallengersStay)]
    [InlineData(PlayRotationMode.SplitTeams)]
    public void Competing_courts_preserve_holds_and_reject_stale_double_booking(PlayRotationMode mode)
    {
        var session = Session(mode, 10, 2);
        session.Start(Now);
        session.StartReadyGames();
        var first = Court(session);
        var second = Court(session, 2);
        session.FinishGame(first.Id, Now, PlayTeam.A);
        Assert.All(Ids(second), id => Assert.DoesNotContain(id, Next(session, first)));
        session.FinishGame(second.Id, Now.AddSeconds(1), PlayTeam.B);
        Assert.Equal(1, session.RotationPreview().Court);
        var stale = Next(session, second);
        session.StartNextGame(first.Id, Next(session, first), false, Now.AddSeconds(1));
        session.StartReadyGames();
        Assert.True(second.IsCurrent);
        Assert.All(Ids(second), id => Assert.Equal(PlayPlayerState.Playing, session.Players.Single(p => p.Id == id).State));
        Assert.Throws<PlayConflictException>(() => session.StartNextGame(second.Id, stale, true, Now.AddSeconds(1)));
        session.StartNextGame(second.Id, Next(session, second), false, Now.AddSeconds(1));
        session.StartReadyGames();
        Assert.Equal(8, session.Matches.Where(m => m.IsCurrent).SelectMany(Ids).Distinct().Count());
    }

    [Theory]
    [InlineData(PlayRotationMode.WinnersStay, 4)]
    [InlineData(PlayRotationMode.ChallengersStay, 5)]
    [InlineData(PlayRotationMode.SplitTeams, 3)]
    public void Small_rosters_do_not_invent_players_and_manual_override_stays_available(PlayRotationMode mode, int count)
    {
        var session = Session(mode, count);
        session.Start(Now);
        session.StartReadyGames();
        if (count < 4)
        {
            Assert.Empty(session.Matches);
            Assert.Empty(session.RotationPreview().Next);
            Assert.Equal(1, session.RotationPreview().Needed);
            return;
        }
        var match = Court(session);
        session.FinishGame(match.Id, Now, PlayTeam.A);
        Assert.Equal(4, Next(session, match).Distinct().Count());
        var manual = Ids(match).Reverse().ToArray();
        session.StartNextGame(match.Id, manual, true, Now);
        session.StartReadyGames();
        Assert.Equal(manual, Ids(Court(session)));
        session.End(Now);
        Assert.Throws<PlayConflictException>(() => session.FinishGame(Court(session).Id, Now));
        Assert.Equal(PlayMatchStatus.Completed, match.Status);
    }
}
