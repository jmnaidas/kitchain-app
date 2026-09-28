using System.Text.Json;
using Kitchain.Domain.Play;

namespace Kitchain.Tests.Play;

public sealed class PlayCourtPlanTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    internal static PlaySession Session(int players = 16, int courts = 2)
    {
        var s = new PlaySession(Guid.NewGuid(), "ABCDEF", "Independent courts", new(2026, 9, 29), new(18, 0), new(22, 0), courts, null, Now);
        for (var i = 1; i <= players; i++) s.AddGuest("Player " + i, Now);
        foreach (var court in s.NextRound().Courts)
            for (var position = 1; position <= 4; position++)
                s.EditNextRound(court.MatchId, position, Player(s, (court.CourtNumber - 1) * 4 + position), s.NextRoundRevision, Now);
        s.Start(Now); s.StartReadyGames();
        return s;
    }
    private static Guid Player(PlaySession s, int number) => s.Players.Single(p => p.DisplayName == "Player " + number).Id;
    private static PlayMatch Current(PlaySession s, int court) => s.Matches.Single(m => m.IsCurrent && m.CourtNumber == court);
    private static PlayPlannedCourt Plan(PlaySession s, int court) => s.NextRound().Courts.Single(c => c.CourtNumber == court);
    private static Guid[] Slots(PlayMatch m) => m.Players.OrderBy(p => p.Position).Select(p => p.PlayerId).ToArray();
    private static void Arrange(PlaySession s, int court, params int[] players)
    {
        var id = Current(s, court).Id;
        for (var i = 0; i < players.Length; i++) s.EditNextRound(id, i + 1, Player(s, players[i]), s.NextRoundRevision, Now);
    }
    private static void Finalize(PlaySession s, int court) => s.FinalizeNextRound(s.NextRoundRevision, Now, Current(s, court).Id);
    private static void Proceed(PlaySession s, int court)
    {
        var id = Current(s, court).Id;
        s.StartNextGame(id, s.NextLineup(id).Select(p => p.Id).ToArray(), false, Now);
    }

    [Fact]
    public void Sixteen_players_court_one_cycles_and_plans_again_while_court_two_keeps_playing()
    {
        var s = Session(); Arrange(s, 1, 9, 10, 11, 12); Arrange(s, 2, 13, 14, 15, 16);
        var second = Current(s, 2); var secondSlots = Slots(second); var secondPlan = JsonSerializer.Serialize(Plan(s, 2));
        Finalize(s, 1);
        Assert.True(Plan(s, 1).Finalized); Assert.False(Plan(s, 2).Finalized);
        var approved = Plan(s, 1).PlayerIds;
        s.FinishGame(Current(s, 1).Id, Now, PlayTeam.A); Proceed(s, 1);
        var ready = Current(s, 1);
        Assert.Equal(PlayMatchStatus.Ready, ready.Status); Assert.Equal(approved, Slots(ready));
        Assert.Equal(PlayMatchStatus.Active, second.Status); Assert.Equal(secondSlots, Slots(second));
        s.StartGame(ready.Id, ready.LineupRevision, Now);
        Assert.Equal(ready.Id, Plan(s, 1).MatchId);
        Assert.False(Plan(s, 1).Finalized);
        Assert.Equal(secondPlan, JsonSerializer.Serialize(Plan(s, 2)));
        Arrange(s, 1, 1, 2, 3, 4); Finalize(s, 1);
        Assert.True(Plan(s, 1).Finalized); Assert.Equal(PlayMatchStatus.Active, second.Status);
        Assert.Equal(8, s.NextRound().Courts.SelectMany(c => c.PlayerIds).Distinct().Count());
        Assert.Single(s.Matches, m => m.Status == PlayMatchStatus.Completed);
    }

    [Fact]
    public void Edit_reset_and_finalize_affect_only_the_target_except_explicit_cross_court_swaps()
    {
        var s = Session(20); Finalize(s, 2); var second = JsonSerializer.Serialize(Plan(s, 2));
        var first = Plan(s, 1);
        s.EditNextRound(first.MatchId, 1, first.PlayerIds[2], s.NextRoundRevision, Now);
        Assert.Equal(second, JsonSerializer.Serialize(Plan(s, 2)));
        s.ResetNextRound(s.NextRoundRevision, Now, first.MatchId);
        Assert.Equal(second, JsonSerializer.Serialize(Plan(s, 2)));
        Finalize(s, 1); Assert.Equal(second, JsonSerializer.Serialize(Plan(s, 2)));
        s.EditNextRound(first.MatchId, 1, Plan(s, 2).PlayerIds[0], s.NextRoundRevision, Now);
        Assert.False(Plan(s, 1).Finalized); Assert.False(Plan(s, 2).Finalized);
        Assert.Equal(8, s.NextRound().Courts.SelectMany(c => c.PlayerIds).Distinct().Count());
    }

    [Fact]
    public void Shared_player_dependency_blocks_only_that_court_until_the_player_is_released()
    {
        var s = Session(24, 3);
        Arrange(s, 1, 13, 14, 15, 5); Arrange(s, 2, 17, 18, 19, 20); Arrange(s, 3, 21, 22, 23, 24);
        Finalize(s, 1); Finalize(s, 3);
        var waiting = Plan(s, 1); s.FinishGame(waiting.MatchId, Now);
        Assert.Empty(s.NextLineup(waiting.MatchId));
        Assert.Throws<PlayConflictException>(() => s.StartNextGame(waiting.MatchId, waiting.PlayerIds, false, Now));
        s.FinishGame(Current(s, 3).Id, Now); Proceed(s, 3);
        Assert.Equal(PlayMatchStatus.Ready, Current(s, 3).Status);
        Assert.Equal(PlayMatchStatus.Active, Current(s, 2).Status);
        s.FinishGame(Current(s, 2).Id, Now); Proceed(s, 2);
        Assert.True(Plan(s, 1).Finalized); Assert.Equal(waiting.PlayerIds, s.NextLineup(waiting.MatchId).Select(p => p.Id));
        Proceed(s, 1);
        Assert.Equal(waiting.PlayerIds, Slots(Current(s, 1)));
        Assert.Equal(12, s.Matches.Where(m => m.IsCurrent).SelectMany(Slots).Distinct().Count());
    }

    [Fact]
    public void A_saved_dependency_survives_its_player_becoming_ready_on_another_court()
    {
        var s = Session(); Arrange(s, 1, 9, 10, 11, 5); Arrange(s, 2, 13, 14, 15, 16); Finalize(s, 1); Finalize(s, 2);
        var first = Plan(s, 1); s.FinishGame(Current(s, 2).Id, Now); Proceed(s, 2);
        var ready = Current(s, 2);
        s.ChangeLineupSlot(ready.Id, 1, Player(s, 5), ready.LineupRevision, Now);
        s.FinishGame(first.MatchId, Now);
        Assert.True(Plan(s, 1).Finalized); Assert.Equal(first.PlayerIds, Plan(s, 1).PlayerIds);
        Assert.Empty(s.NextLineup(first.MatchId));
        Assert.Throws<PlayConflictException>(() => s.StartNextGame(first.MatchId, first.PlayerIds, false, Now));
        Assert.Equal(8, s.Matches.Where(m => m.IsCurrent).SelectMany(Slots).Distinct().Count());
    }

    [Fact]
    public void Stale_edits_and_resets_are_rejected_and_legacy_finalized_json_keeps_its_intent()
    {
        var s = Session(); var initial = s.NextRound(); var stale = s.NextRoundRevision;
        Finalize(s, 1);
        Assert.Throws<PlayConflictException>(() => s.FinalizeNextRound(stale, Now, Current(s, 2).Id));
        Assert.Throws<PlayConflictException>(() => s.ResetNextRound(stale, Now, Current(s, 1).Id));
        typeof(PlaySession).GetProperty(nameof(PlaySession.NextRoundJson))!.SetValue(s,
            JsonSerializer.Serialize(new { Finalized = true, Courts = initial.Courts.Select(c => new { c.MatchId, c.CourtNumber, c.PlayerIds }) }));
        Assert.All(s.NextRound().Courts, c => Assert.True(c.Finalized));
        s.ResetNextRound(s.NextRoundRevision, Now, Current(s, 1).Id);
        Assert.False(Plan(s, 1).Finalized); Assert.True(Plan(s, 2).Finalized);
    }

    [Fact]
    public void Removing_one_planned_player_does_not_discard_another_finalized_court()
    {
        var s = Session(20); s.FinalizeNextRound(s.NextRoundRevision, Now);
        var second = JsonSerializer.Serialize(Plan(s, 2));
        var removed = Plan(s, 1).PlayerIds.First(id => s.Players.Single(p => p.Id == id).State == PlayPlayerState.Waiting);
        s.RemoveGuest(removed, Now);
        Assert.Equal(second, JsonSerializer.Serialize(Plan(s, 2)));
        Assert.False(Plan(s, 1).Finalized);
        Assert.DoesNotContain(removed, s.NextRound().Courts.SelectMany(c => c.PlayerIds));
    }
}
