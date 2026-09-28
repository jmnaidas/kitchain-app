using System.Text.Json;
using Kitchain.Domain.Play;

namespace Kitchain.Tests.Play;

public sealed class PlaySkillOpeningTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static PlaySession Session(int players = 9, int courts = 2, PlayRotationMode mode = PlayRotationMode.BalancedRotation)
    {
        var s = new PlaySession(Guid.NewGuid(), "ABCDEF", "Opening", new(2026, 9, 28), new(18, 0), new(22, 0),
            courts, null, Now, rotationMode: mode);
        for (var i = 0; i < players; i++) s.AddGuest("Player " + i, Now);
        return s;
    }
    private static Guid[] Ids(PlaySession s) => s.NextRound().Courts.SelectMany(c => c.PlayerIds).ToArray();
    private static string Plan(PlaySession s) => JsonSerializer.Serialize(s.NextRound());
    private static void Skill(PlaySession s, PlaySessionPlayer p, PlaySkillLevel? level) =>
        s.SetSkillLevel(p.Id, level, s.NextRoundRevision, Now);

    [Theory]
    [InlineData(1, 2, 2)]
    [InlineData(1, 9, 4)]
    [InlineData(2, 18, 8)]
    [InlineData(3, 18, 12)]
    [InlineData(3, 10, 10)]
    public void Opening_is_court_aware_unique_and_does_not_credit_play(int courts, int players, int expected)
    {
        var s = Session(players, courts);
        Assert.Equal(expected, Ids(s).Length);
        Assert.Equal(expected, Ids(s).Distinct().Count());
        Assert.Equal(expected, s.RotationPreview().Next.Count);
        Assert.Equal(players - expected, s.RotationPreview().Waiting.Count);
        Assert.All(s.Players, p => { Assert.Null(p.SkillLevel); Assert.Equal(0, p.AdjustedGamesStarted); });
        Assert.Empty(s.Matches);
        var plan = s.NextRound();
        s.Start(Now);
        Assert.Equal(plan.Courts.Count(c => c.PlayerIds.Length == 4), s.Matches.Count);
        foreach (var m in s.Matches)
        {
            Assert.Equal(PlayMatchStatus.Ready, m.Status);
            Assert.Equal(plan.Courts.Single(c => c.CourtNumber == m.CourtNumber).PlayerIds,
                m.Players.OrderBy(p => p.Position).Select(p => p.PlayerId));
        }
    }

    [Fact]
    public void Skills_are_optional_validated_revision_guarded_and_editable_until_ended()
    {
        var s = Session(4, 1); var p = s.Players.First();
        foreach (var level in Enum.GetValues<PlaySkillLevel>()) { Skill(s, p, level); Assert.Equal(level, p.SkillLevel); }
        var stale = s.NextRoundRevision;
        Skill(s, p, null); Assert.Null(p.SkillLevel);
        Assert.Throws<PlayConflictException>(() => s.SetSkillLevel(p.Id, PlaySkillLevel.Advanced, stale, Now));
        Assert.Throws<ArgumentException>(() => Skill(s, p, (PlaySkillLevel)0));
        s.Start(Now); s.StartReadyGames(); Skill(s, p, PlaySkillLevel.Novice);
        Assert.Equal(PlaySkillLevel.Novice, p.SkillLevel);
        s.End(Now);
        Assert.Throws<PlayConflictException>(() => Skill(s, p, null));
    }

    [Fact]
    public void Balanced_pairs_five_plus_one_against_four_plus_two_in_opening_and_next_round()
    {
        var s = Session(4, 1);
        var levels = new[] { PlaySkillLevel.Advanced, PlaySkillLevel.HighIntermediate, PlaySkillLevel.Novice, PlaySkillLevel.Beginner };
        foreach (var (p, i) in s.Players.Select((p, i) => (p, i))) Skill(s, p, levels[i]);
        void Balanced()
        {
            var ids = Ids(s); var players = ids.Select(id => s.Players.Single(p => p.Id == id)).ToArray();
            Assert.Equal(6, players.Take(2).Sum(p => (int)p.SkillLevel!));
            Assert.Equal(6, players.Skip(2).Sum(p => (int)p.SkillLevel!));
        }
        Balanced(); s.Start(Now); s.StartReadyGames(); Balanced();
    }

    [Fact]
    public void No_skill_matches_fair_rotation_exactly_and_partial_skill_is_deterministic()
    {
        var s = Session(12, 2, PlayRotationMode.FairRotation);
        var fair = Plan(s);
        s.EditDetails(s.Name, s.SessionDate, s.StartTime, s.EndDate, s.EndTime, 2, null, s.Mode, Now, PlayRotationMode.BalancedRotation);
        Assert.Equal(fair, Plan(s));
        Skill(s, s.Players.First(), PlaySkillLevel.Advanced);
        Skill(s, s.Players.Skip(1).First(), PlaySkillLevel.Beginner);
        Assert.Equal(Plan(s), Plan(s));
        Assert.Equal(8, Ids(s).Distinct().Count());
        Assert.Contains(Ids(s), id => s.Players.Single(p => p.Id == id).SkillLevel is null);
        Assert.Equal(JsonSerializer.Deserialize<PlayRoundPlan>(fair)!.Courts.SelectMany(c => c.PlayerIds).Order(), Ids(s).Order());
        foreach (var p in s.Players) Skill(s, p, PlaySkillLevel.Novice);
        Assert.Equal(fair, Plan(s)); // Equal skill is also a tie: keep the Fair Rotation arrangement.
    }

    [Fact]
    public void Multiple_courts_share_skill_levels_without_changing_the_fairly_selected_pool()
    {
        var s = Session(12);
        var selected = Ids(s);
        foreach (var (id, i) in selected.Select((id, i) => (id, i)))
            Skill(s, s.Players.Single(p => p.Id == id), i < 4 ? PlaySkillLevel.Advanced : PlaySkillLevel.Beginner);
        foreach (var p in s.Players.Where(p => !selected.Contains(p.Id))) Skill(s, p, PlaySkillLevel.Advanced);
        Assert.Equal(selected.Order(), Ids(s).Order());
        Assert.All(s.NextRound().Courts, c =>
        {
            var levels = c.PlayerIds.Select(id => s.Players.Single(p => p.Id == id).SkillLevel).ToArray();
            Assert.Contains(PlaySkillLevel.Advanced, levels);
            Assert.Contains(PlaySkillLevel.Beginner, levels);
        });
        s.Start(Now); s.StartReadyGames();
        var neverPlayed = s.Players.Where(p => p.AdjustedGamesStarted == 0).Select(p => p.Id).ToArray();
        Assert.All(neverPlayed, id => Assert.Contains(id, Ids(s)));
    }

    [Fact]
    public void Same_cross_and_waiting_edits_survive_add_skill_and_start_exactly()
    {
        var s = Session(); var original = s.NextRound(); var a = original.Courts[0]; var b = original.Courts[1];
        s.EditNextRound(a.MatchId, 1, a.PlayerIds[2], s.NextRoundRevision, Now);
        Assert.Equal(a.PlayerIds[0], s.NextRound().Courts[0].PlayerIds[2]);
        s.EditNextRound(a.MatchId, 1, b.PlayerIds[1], s.NextRoundRevision, Now);
        Assert.Equal(a.PlayerIds[2], s.NextRound().Courts[1].PlayerIds[1]);
        var waiting = s.Players.Single(p => !Ids(s).Contains(p.Id));
        s.EditNextRound(a.MatchId, 2, waiting.Id, s.NextRoundRevision, Now);
        var approved = Plan(s); var revision = s.NextRoundRevision;
        s.AddGuest("Late arrival", Now);
        Skill(s, waiting, PlaySkillLevel.Advanced);
        Assert.Equal(approved, Plan(s));
        Assert.Throws<PlayConflictException>(() => s.EditNextRound(a.MatchId, 1, waiting.Id, revision, Now));
        Assert.Throws<PlayConflictException>(() => s.FinalizeNextRound(s.NextRoundRevision, Now));
        var plan = s.NextRound(); s.Start(Now);
        foreach (var m in s.Matches)
            Assert.Equal(plan.Courts.Single(c => c.CourtNumber == m.CourtNumber).PlayerIds,
                m.Players.OrderBy(p => p.Position).Select(p => p.PlayerId));
        Assert.All(s.Players, p => Assert.Equal(0, p.AdjustedGamesStarted));
        var ready = s.Matches.First(); var slots = ready.Players.OrderBy(p => p.Position).ToArray();
        s.ChangeLineupSlot(ready.Id, 1, slots[2].PlayerId, ready.LineupRevision, Now);
        Skill(s, waiting, PlaySkillLevel.Beginner);
        Assert.Equal(slots[2].PlayerId, ready.Players.Single(p => p.Position == 1).PlayerId);
    }

    [Fact]
    public void Removed_opening_player_is_repaired_without_discarding_other_manual_slots()
    {
        var s = Session(); var a = s.NextRound().Courts[0];
        s.EditNextRound(a.MatchId, 1, a.PlayerIds[2], s.NextRoundRevision, Now);
        var before = s.NextRound();
        var removed = before.Courts[0].PlayerIds[1];
        s.RemoveGuest(removed, Now);
        Assert.DoesNotContain(removed, Ids(s)); Assert.Equal(8, Ids(s).Distinct().Count());
        Assert.Equal(before.Courts[0].PlayerIds[0], s.NextRound().Courts[0].PlayerIds[0]);
        Assert.Equal(before.Courts[1].PlayerIds, s.NextRound().Courts[1].PlayerIds);
        s.RemoveGuest(s.NextRound().Courts[0].PlayerIds[0], Now);
        Assert.Equal(7, Ids(s).Distinct().Count());
        s.AddGuest("Replacement", Now);
        Assert.Equal(8, Ids(s).Distinct().Count());
    }

    [Fact]
    public void Balanced_finalized_plan_does_not_rebalance_when_skills_change()
    {
        var s = Session(12); s.Start(Now); s.StartReadyGames();
        var a = s.NextRound().Courts[0];
        s.EditNextRound(a.MatchId, 1, a.PlayerIds[2], s.NextRoundRevision, Now);
        s.FinalizeNextRound(s.NextRoundRevision, Now);
        var saved = Plan(s);
        foreach (var p in s.Players) Skill(s, p, PlaySkillLevel.Advanced);
        Assert.Equal(saved, Plan(s));
    }
}
