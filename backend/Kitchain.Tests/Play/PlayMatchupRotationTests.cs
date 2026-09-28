using System.Reflection;
using System.Text.Json;
using Kitchain.Domain.Play;

namespace Kitchain.Tests.Play;

public sealed class PlayMatchupRotationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static PlaySession Session(int count = 8, int courts = 1)
    {
        var s = new PlaySession(Guid.NewGuid(), "ABCDEF", "Coverage", new(2026, 9, 29),
            new(18, 0), new(22, 0), courts, null, Now);
        for (var i = 0; i < count; i++) s.AddGuest("Player " + i, Now);
        return s;
    }

    // Isolate relationship fixtures from participation counters, using the same match slots
    // and completion methods as the aggregate. No redundant relationship model is persisted.
    private static PlayMatch Match(PlaySession s, PlayMatchStatus status, params PlaySessionPlayer[] slots)
    {
        var match = (PlayMatch)Activator.CreateInstance(typeof(PlayMatch), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [s.Id, 1, slots, Now], null)!;
        if (status != PlayMatchStatus.Ready)
            typeof(PlayMatch).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(match, [Now]);
        if (status == PlayMatchStatus.Completed)
            typeof(PlayMatch).GetMethod("Complete", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(match, [Now, null]);
        return match;
    }

    private static PlaySessionPlayer[] Pair(PlaySession s, PlaySessionPlayer[] players, PlayMatch[] history, bool balanced = false) =>
        (PlaySessionPlayer[])typeof(PlaySession).Assembly.GetType("Kitchain.Domain.Play.PlayTeamPairing")!
            .GetMethod(balanced ? "RecommendBalanced" : "Recommend")!.Invoke(null,
                balanced ? [s.Id, players, history, s.Id] : [s.Id, players, history, s.Id, null])!;
    private static bool Partners(PlaySessionPlayer[] lineup, PlaySessionPlayer a, PlaySessionPlayer b) =>
        Array.IndexOf(lineup, a) / 2 == Array.IndexOf(lineup, b) / 2;

    [Fact]
    public void Never_teamed_then_lower_lifetime_partner_counts_take_priority()
    {
        var s = Session(); var p = s.Players.Take(4).ToArray();
        var repeated = Enumerable.Range(0, 3).Select(_ => Match(s, PlayMatchStatus.Completed, p)).ToList();
        Assert.False(Partners(Pair(s, p, repeated.ToArray()), p[0], p[1]));
        repeated.Add(Match(s, PlayMatchStatus.Completed, p[0], p[2], p[1], p[3]));
        repeated.AddRange(Enumerable.Range(0, 2).Select(_ => Match(s, PlayMatchStatus.Completed, p[0], p[3], p[1], p[2])));
        var chosen = Pair(s, p, repeated.ToArray());
        Assert.True(Partners(chosen, p[0], p[2]));
        Assert.Equal(chosen, Pair(s, p, repeated.ToArray()));
    }

    [Fact]
    public void Opponent_counts_break_equal_teammate_cost_using_canonical_team_slots()
    {
        var s = Session(); var p = s.Players.ToArray();
        // None of the selected four has partnered. Only 0 and 2 have faced each other.
        var history = new[] { Match(s, PlayMatchStatus.Completed, p[0], p[4], p[2], p[5]) };
        var chosen = Pair(s, p.Take(4).ToArray(), history);
        Assert.True(Partners(chosen, p[0], p[2])); // Avoid that opponent repeat.
        Assert.All(chosen.Chunk(2), team => Assert.DoesNotContain(history, m =>
            m.Players.Count(slot => team.Any(p => p.Id == slot.PlayerId)) == 2 &&
            m.Players.Where(slot => team.Any(p => p.Id == slot.PlayerId)).Select(slot => slot.Team).Distinct().Count() == 1));
    }

    [Theory]
    [InlineData(PlayMatchStatus.Ready)]
    [InlineData(PlayMatchStatus.Active)]
    public void Incomplete_matches_do_not_change_fair_or_balanced_pairing(PlayMatchStatus status)
    {
        var s = Session(); var p = s.Players.Take(4).ToArray();
        var history = new[] { Match(s, status, p) };
        Assert.Equal(Pair(s, p, []), Pair(s, p, history));
        Assert.Equal(Pair(s, p, [], true), Pair(s, p, history, true));
    }

    [Fact]
    public void Fair_ignores_skill_and_balanced_protects_large_skill_gaps_but_varies_similar_games()
    {
        var s = Session(); var p = s.Players.Take(4).ToArray();
        var history = new[] { Match(s, PlayMatchStatus.Completed, p[0], p[2], p[1], p[3]) };
        var fair = Pair(s, p, history);
        for (var i = 0; i < 4; i++) s.SetSkillLevel(p[i].Id,
            i < 2 ? PlaySkillLevel.Advanced : PlaySkillLevel.Novice, s.NextRoundRevision, Now);
        Assert.Equal(fair, Pair(s, p, history));
        var balanced = Pair(s, p, history, true);
        Assert.False(Partners(balanced, p[0], p[1])); // 5+5 versus 1+1 is disallowed.
        Assert.True(Partners(balanced, p[0], p[3])); // Equally balanced, unused partnership.
    }

    [Fact]
    public void Balanced_can_trade_a_small_skill_difference_for_new_partners()
    {
        var s = Session(); var p = s.Players.Take(4).ToArray();
        var levels = new[] { 5, 4, 2, 1 };
        for (var i = 0; i < 4; i++) s.SetSkillLevel(p[i].Id, (PlaySkillLevel)levels[i], s.NextRoundRevision, Now);
        var history = new[] { Match(s, PlayMatchStatus.Completed, p[0], p[3], p[1], p[2]) };
        Assert.True(Partners(Pair(s, p, [], true), p[0], p[3])); // Opening: exact balance.
        Assert.True(Partners(Pair(s, p, history, true), p[0], p[2])); // 7 versus 5: a small allowed trade.
    }

    [Fact]
    public void Matchups_repeated_game_fixture_and_recommendation_agree()
    {
        var s = Session(); var p = s.Players.Take(4).ToArray();
        // Same completed slots as play-matchups.spec.ts: 01/23 twice, then 02/13.
        // Player 0's partner counts are 2,1,0; opponent counts are 1,2,3.
        var history = new[] { Match(s, PlayMatchStatus.Completed, p), Match(s, PlayMatchStatus.Completed, p),
            Match(s, PlayMatchStatus.Completed, p[0], p[2], p[1], p[3]) };
        var chosen = Pair(s, p, history);
        Assert.True(Partners(chosen, p[0], p[3]));
        Assert.True(Partners(chosen, p[1], p[2]));
    }

    [Fact]
    public void Completed_court_immediately_changes_unsaved_recommendation_without_waiting_for_other_court()
    {
        var s = Session(8, 2); s.Start(Now); s.StartReadyGames();
        var first = s.Matches.Single(m => m.CourtNumber == 1);
        var other = s.Matches.Single(m => m.CourtNumber == 2);
        var slots = first.Players.OrderBy(p => p.Position).Select(slot => s.Players.Single(p => p.Id == slot.PlayerId)).ToArray();
        s.FinishGame(first.Id, Now);
        var next = s.NextLineup(first.Id).ToArray();
        Assert.False(Partners(next, slots[0], slots[1]));
        Assert.Equal(PlayMatchStatus.Active, other.Status);
        Assert.Equal(slots.Select(p => p.Id).Order(), next.Select(p => p.Id).Order());
        s.StartNextGame(first.Id, next.Select(p => p.Id).ToArray(), false, Now);
        Assert.Equal(8, s.Matches.Where(m => m.IsCurrent).SelectMany(m => m.Players).Select(p => p.PlayerId).Distinct().Count());
    }

    [Fact]
    public void Saved_manual_courts_remain_authoritative_when_another_match_completes()
    {
        var s = Session(12, 2); s.Start(Now); s.StartReadyGames();
        var court = s.NextRound().Courts[0];
        s.EditNextRound(court.MatchId, 1, court.PlayerIds[2], s.NextRoundRevision, Now);
        s.FinalizeNextRound(s.NextRoundRevision, Now, court.MatchId);
        var saved = JsonSerializer.Serialize(s.NextRound());
        s.FinishGame(s.Matches.Single(m => m.CourtNumber == 2).Id, Now);
        Assert.Equal(saved, JsonSerializer.Serialize(s.NextRound()));
    }

    [Fact]
    public void Clearly_due_players_keep_their_turn_despite_repeated_partnerships_and_resting_players_stay_out()
    {
        var s = Session(9); s.Start(Now); s.StartReadyGames();
        var resting = s.WaitingQueue.Last(); s.Rest(resting.Id, Now);
        var first = s.Matches.Single();
        var due = s.WaitingQueue.Select(p => p.Id).ToArray();
        s.FinishGame(first.Id, Now);
        var next = s.NextLineup(first.Id);
        Assert.Equal(due.Order(), next.Select(p => p.Id).Order());
        Assert.DoesNotContain(next, p => p.Id == resting.Id);
        Assert.All(next, p => Assert.Equal(0, p.AdjustedGamesStarted));
        var duePlayers = next.ToArray();
        var repeated = Enumerable.Range(0, 3).Select(_ => Match(s, PlayMatchStatus.Completed, duePlayers)).ToArray();
        var selected = (PlaySessionPlayer[])typeof(PlaySession).Assembly.GetType("Kitchain.Domain.Play.PlayFairRotation")!
            .GetMethod("Select")!.Invoke(null, [s.EligibleNextPlayers(first.Id), repeated, first.Id, 4])!;
        Assert.Equal(due.Order(), selected.Select(p => p.Id).Order());
    }
}
