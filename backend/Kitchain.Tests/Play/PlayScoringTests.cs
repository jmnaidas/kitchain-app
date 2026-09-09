using Kitchain.Domain.Play;

namespace Kitchain.Tests.Play;

public sealed class PlayScoringTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 0, 0, 0, TimeSpan.Zero);
    private static PlaySession Session()
    {
        var session = new PlaySession(Guid.NewGuid(), "ABCDEF", "Crew", new(2026, 9, 10), new(18, 0), new(21, 0), 2, null, Now);
        for (var i = 1; i <= 14; i++) session.AddGuest($"Player {i}", Now);
        session.Start(Now);
        return session;
    }

    [Fact]
    public void Opening_0_0_2_and_side_out_transitions_award_points_only_to_serving_team()
    {
        var session = Session();
        var match = session.Matches.First();
        Assert.Equal((0, 0, PlayTeam.A, 2), (match.TeamAScore, match.TeamBScore, match.ServingTeam, match.CurrentServerNumber));
        session.RecordRally(match.Id, PlayTeam.A, Now);
        Assert.Equal((1, 0, PlayTeam.A, 2), (match.TeamAScore, match.TeamBScore, match.ServingTeam, match.CurrentServerNumber));
        session.RecordRally(match.Id, PlayTeam.B, Now);
        Assert.Equal((1, 0, PlayTeam.B, 1), (match.TeamAScore, match.TeamBScore, match.ServingTeam, match.CurrentServerNumber));
        session.RecordRally(match.Id, PlayTeam.B, Now);
        Assert.Equal((1, 1, PlayTeam.B, 1), (match.TeamAScore, match.TeamBScore, match.ServingTeam, match.CurrentServerNumber));
        session.RecordRally(match.Id, PlayTeam.A, Now);
        Assert.Equal((1, 1, PlayTeam.B, 2), (match.TeamAScore, match.TeamBScore, match.ServingTeam, match.CurrentServerNumber));
        session.RecordRally(match.Id, PlayTeam.A, Now);
        Assert.Equal((1, 1, PlayTeam.A, 1), (match.TeamAScore, match.TeamBScore, match.ServingTeam, match.CurrentServerNumber));
    }

    [Theory]
    [InlineData(10, 0, true)]
    [InlineData(10, 9, true)]
    [InlineData(10, 10, false)]
    [InlineData(11, 10, true)]
    [InlineData(13, 13, false)]
    [InlineData(14, 13, true)]
    public void Legal_winning_rally_uses_target_and_two_point_margin(int score, int opponent, bool completed)
    {
        var session = Session();
        var match = session.Matches.First();
        session.CorrectScore(match.Id, score, opponent, PlayTeam.A, 1, Now);
        session.RecordRally(match.Id, PlayTeam.A, Now);
        Assert.Equal(score + 1, match.TeamAScore);
        Assert.Equal(completed ? PlayMatchStatus.Completed : PlayMatchStatus.Active, match.Status);
    }

    [Fact]
    public void Winning_team_B_rotates_existing_queue_once_and_leaves_other_court_unchanged()
    {
        var session = Session();
        var match = session.Matches.Single(m => m.CourtNumber == 2);
        var other = session.Matches.Single(m => m.CourtNumber == 1);
        var queue = session.WaitingQueue.Select(p => p.Id).ToArray();
        var returning = match.Players.OrderBy(p => p.Position).Select(p => p.PlayerId).ToArray();
        session.CorrectScore(match.Id, 9, 10, PlayTeam.B, 2, Now);
        session.RecordRally(match.Id, PlayTeam.B, Now.AddMinutes(1));
        Assert.Equal(11, match.TeamBScore);
        Assert.Equal(Now.AddMinutes(1), match.CompletedAt);
        var replacement = session.Matches.Single(m => m.CourtNumber == 2 && m.Status == PlayMatchStatus.Active);
        Assert.Equal(queue.Take(4), replacement.Players.OrderBy(p => p.Position).Select(p => p.PlayerId));
        Assert.Equal((0, 0, PlayTeam.A, 2), (replacement.TeamAScore, replacement.TeamBScore, replacement.ServingTeam, replacement.CurrentServerNumber));
        Assert.Equal(queue.Skip(4).Concat(returning), session.WaitingQueue.Select(p => p.Id));
        Assert.Equal((PlayMatchStatus.Active, 0, 0), (other.Status, other.TeamAScore, other.TeamBScore));
        Assert.Throws<PlayConflictException>(() => session.RecordRally(match.Id, PlayTeam.B, Now.AddMinutes(2)));
        Assert.Throws<PlayConflictException>(() => session.CorrectScore(match.Id, 0, 0, PlayTeam.A, 2, Now.AddMinutes(2)));
        Assert.Equal(3, session.Matches.Count);
    }

    [Fact]
    public void Correction_repairs_state_without_completion_and_rejects_invalid_values_and_lifecycle()
    {
        var session = Session();
        var match = session.Matches.First();
        session.CorrectScore(match.Id, 15, 13, PlayTeam.B, 1, Now);
        Assert.Equal((15, 13, PlayTeam.B, 1), (match.TeamAScore, match.TeamBScore, match.ServingTeam, match.CurrentServerNumber));
        Assert.Equal(PlayMatchStatus.Active, match.Status);
        Assert.Throws<ArgumentException>(() => session.CorrectScore(match.Id, -1, 0, PlayTeam.A, 1, Now));
        Assert.Throws<ArgumentException>(() => session.CorrectScore(match.Id, 0, -1, PlayTeam.A, 1, Now));
        Assert.Throws<ArgumentException>(() => session.CorrectScore(match.Id, 0, 0, (PlayTeam)99, 1, Now));
        Assert.Throws<ArgumentException>(() => session.CorrectScore(match.Id, 0, 0, PlayTeam.A, 3, Now));
        Assert.Throws<ArgumentException>(() => session.RecordRally(match.Id, (PlayTeam)99, Now));
        Assert.Throws<PlayConflictException>(() => session.RecordRally(Guid.NewGuid(), PlayTeam.A, Now));
        Assert.Equal((15, 13, PlayTeam.B, 1), (match.TeamAScore, match.TeamBScore, match.ServingTeam, match.CurrentServerNumber));
        session.End(Now);
        Assert.Throws<PlayConflictException>(() => session.RecordRally(match.Id, PlayTeam.A, Now));
        Assert.Throws<PlayConflictException>(() => session.CorrectScore(match.Id, 0, 0, PlayTeam.A, 1, Now));
    }
}
