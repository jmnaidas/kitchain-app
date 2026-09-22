using Kitchain.Application.Play;
using Kitchain.Domain.Play;

namespace Kitchain.Tests.Play;

public sealed class PlayReadyLineupTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static PlaySession Session(int players = 10, PlaySessionMode mode = PlaySessionMode.LiveScoring,
        PlayRotationMode rotation = PlayRotationMode.FairRotation)
    {
        var s = new PlaySession(Guid.NewGuid(), "ABCDEF", "Ready test", new(2026, 9, 21), new(18, 0), new(22, 0), 2, null, Now, mode, rotationMode: rotation);
        for (var i = 0; i < players; i++) s.AddGuest("Player " + i, Now);
        s.Start(Now);
        return s;
    }
    private static Guid[] Ids(PlayMatch m) => m.Players.OrderBy(p => p.Position).Select(p => p.PlayerId).ToArray();

    [Theory]
    [InlineData(PlaySessionMode.LiveScoring)]
    [InlineData(PlaySessionMode.QueueOnly)]
    public void Start_session_reserves_unique_ready_lineups_without_participation(PlaySessionMode mode)
    {
        var s = Session(mode: mode);
        Assert.Equal(2, s.Matches.Count);
        Assert.All(s.Matches, m => { Assert.Equal(PlayMatchStatus.Ready, m.Status); Assert.Null(m.StartedAt); Assert.Empty(m.Rallies); });
        Assert.Equal(8, s.Matches.SelectMany(m => Ids(m)).Distinct().Count());
        Assert.All(s.Players, p => { Assert.Equal(PlayPlayerState.Waiting, p.State); Assert.Equal(0, p.AdjustedGamesStarted); Assert.Equal(0, p.MissedOpportunities); });
        Assert.Equal(2, s.WaitingQueue.Count);
        Assert.Equal(8, s.RotationPreview().Next.Count(p => s.Matches.Any(m => Ids(m).Contains(p.Id))));
        var match = s.Matches.First();
        Assert.Throws<PlayConflictException>(() => s.RecordRally(match.Id, PlayTeam.A, Now));
        Assert.Throws<PlayConflictException>(() => s.FinishGame(match.Id, Now));
    }

    [Fact]
    public void Same_four_can_form_all_pairings_and_waiting_replacement_keeps_original_tickets()
    {
        var s = Session(); var m = s.Matches.First(); var original = Ids(m);
        var tickets = s.Players.ToDictionary(p => p.Id, p => (p.QueueOrder, p.WaitingSince));
        s.ChangeLineupSlot(m.Id, 2, original[2], 0, Now);
        Assert.Equal(new[] { original[0], original[2], original[1], original[3] }, Ids(m));
        s.ChangeLineupSlot(m.Id, 2, original[3], 1, Now);
        Assert.Equal(new[] { original[0], original[3], original[1], original[2] }, Ids(m));
        var replacement = s.WaitingQueue.First();
        s.ChangeLineupSlot(m.Id, 2, replacement.Id, 2, Now);
        Assert.Contains(s.WaitingQueue, p => p.Id == original[3]);
        Assert.DoesNotContain(s.WaitingQueue, p => p.Id == replacement.Id);
        Assert.All(s.Players, p => { Assert.Equal(tickets[p.Id], (p.QueueOrder, p.WaitingSince)); Assert.Equal(0, p.AdjustedGamesStarted); });
        s.StartGame(m.Id, 3, Now.AddMinutes(1));
        Assert.Equal(Now.AddMinutes(1), m.StartedAt);
        Assert.Equal(1, replacement.AdjustedGamesStarted);
        Assert.Equal(0, s.Players.Single(p => p.Id == original[3]).AdjustedGamesStarted);
        Assert.All(s.Players.Where(p => Ids(m).Contains(p.Id)), p => Assert.Equal(PlayPlayerState.Playing, p.State));
        Assert.Throws<PlayConflictException>(() => s.StartGame(m.Id, 3, Now.AddMinutes(1)));
        Assert.Throws<PlayConflictException>(() => s.ChangeLineupSlot(m.Id, 1, original[0], 3, Now.AddMinutes(1)));
    }

    [Fact]
    public void Invalid_or_stale_changes_are_atomic_and_reset_restores_recommendation()
    {
        var s = Session(11); var m = s.Matches.First(); var ids = Ids(m);
        var other = s.Matches.Last(); var resting = s.WaitingQueue.First();
        s.Rest(resting.Id, Now);
        foreach (var invalid in new[] { Guid.NewGuid(), Ids(other)[0], resting.Id })
            Assert.Throws<PlayConflictException>(() => s.ChangeLineupSlot(m.Id, 1, invalid, 0, Now));
        Assert.Throws<ArgumentException>(() => s.ChangeLineupSlot(m.Id, 5, ids[0], 0, Now));
        Assert.Equal(ids, Ids(m)); Assert.Equal(0, m.LineupRevision);
        var recommendation = s.ReadyRecommendation(m.Id).Select(p => p.Id).ToArray();
        s.ChangeLineupSlot(m.Id, 2, ids[2], 0, Now);
        Assert.Throws<PlayConflictException>(() => s.StartGame(m.Id, 0, Now));
        Assert.Throws<PlayConflictException>(() => s.ResetRecommendation(m.Id, 0, Now));
        s.ResetRecommendation(m.Id, 1, Now);
        Assert.Equal(recommendation, Ids(m)); Assert.False(m.IsLineupOverridden);
        Assert.Equal(Ids(other), other.Players.OrderBy(p => p.Position).Select(p => p.PlayerId));
        s.End(Now);
        Assert.Throws<PlayConflictException>(() => s.StartGame(m.Id, 2, Now));
    }

    [Theory]
    [InlineData(PlayRotationMode.FairRotation)]
    [InlineData(PlayRotationMode.WinnersStay)]
    [InlineData(PlayRotationMode.ChallengersStay)]
    [InlineData(PlayRotationMode.SplitTeams)]
    public void Next_recommendation_uses_final_started_teams_and_next_game_is_ready(PlayRotationMode mode)
    {
        var s = Session(12, PlaySessionMode.QueueOnly, mode); var m = s.Matches.First();
        var old = Ids(m); var waiting = s.WaitingQueue.First();
        s.ChangeLineupSlot(m.Id, 1, waiting.Id, 0, Now);
        s.ChangeLineupSlot(m.Id, 2, old[2], 1, Now);
        var final = Ids(m);
        s.StartGame(m.Id, 2, Now);
        s.FinishGame(m.Id, Now, PlayTeam.A);
        var next = s.NextLineup(m.Id).Select(p => p.Id).ToArray();
        if (mode == PlayRotationMode.WinnersStay) Assert.All(final.Take(2), id => Assert.Contains(id, next));
        if (mode == PlayRotationMode.ChallengersStay) Assert.All(final.Skip(2), id => Assert.Contains(id, next));
        var games = s.Players.ToDictionary(p => p.Id, p => p.AdjustedGamesStarted);
        s.StartNextGame(m.Id, next, false, Now);
        var ready = s.Matches.Single(x => x.IsCurrent && x.CourtNumber == m.CourtNumber);
        Assert.Equal(PlayMatchStatus.Ready, ready.Status); Assert.Null(ready.StartedAt);
        Assert.All(s.Players, p => Assert.Equal(games[p.Id], p.AdjustedGamesStarted));
        Assert.Equal(final, Ids(m));
        Assert.Throws<PlayConflictException>(() => s.ChangeLineupSlot(m.Id, 1, final[0], 2, Now));
    }
}
