using Kitchain.Application.Play;
using Kitchain.Domain.Play;

namespace Kitchain.Tests.Play;

public sealed class PlayInsightsTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);
    private static PlaySession Session(PlaySessionMode mode = PlaySessionMode.LiveScoring)
    {
        var session = new PlaySession(Guid.NewGuid(), "ABCDEF", "Crew", new(2026, 9, 15), new(18, 0), new(21, 0), 1, null, Now, mode);
        foreach (var name in new[] { "Zoe", "Alex", "Kim", "Bea", "Lee", "Sam", "Pat", "Dev" }) session.AddGuest(name, Now);
        session.Start(Now);

        session.StartReadyGames();
        return session;
    }
    private static PlayMatch Current(PlaySession session) => session.Matches.Single(m => m.IsCurrent);
    private static Guid[] Slots(PlayMatch match) => match.Players.OrderBy(p => p.Position).Select(p => p.PlayerId).ToArray();
    private static async Task<PlayInsights> Read(PlaySession session) => (await new PlaySessionService(
        new Store(session), new PlaySessionServiceTests.SequenceCodes("ABCDEF")).FindAsync("ABCDEF", default))!.Insights;

    [Fact]
    public async Task Counts_completed_participation_recorded_results_and_variety_without_attributing_shots()
    {
        var session = Session();
        var first = Current(session);
        var original = Slots(first);
        var waiting = session.WaitingQueue.Select(p => p.Id).ToArray();
        session.RecordRally(first.Id, PlayTeam.A, Now);
        session.EditRallyCallOut(first.Id, first.Rallies.Single().Id, PlayRallyCallOut.Drive, null, Now);
        session.RecordRally(first.Id, PlayTeam.B, Now);
        var active = await Read(session);
        Assert.Equal(8, active.TotalPlayers);
        Assert.Equal(1, active.NumberOfCourts);
        Assert.Equal(0, active.CompletedGames);
        Assert.Equal(0, active.PlayerAppearances);
        Assert.Equal(0, active.RecordedRallies);
        Assert.All(active.Players, p => Assert.Equal(0, p.GamesPlayed));
        session.CorrectScore(first.Id, 11, 8, PlayTeam.A, 2, Now.AddMinutes(10));
        var initial = await Read(session);
        Assert.Equal(1, initial.CompletedGames);
        Assert.Equal(4, initial.PlayerAppearances);
        Assert.Equal(2, initial.RecordedRallies);
        Assert.Equal(1, initial.TaggedRallies);
        foreach (var slot in first.Players)
        {
            var player = initial.Players.Single(p => p.PlayerId == slot.PlayerId);
            Assert.Equal(1, player.GamesPlayed);
            Assert.Equal(slot.Team == PlayTeam.A ? 1 : 0, player.Wins);
            Assert.Equal(slot.Team == PlayTeam.B ? 1 : 0, player.Losses);
        }
        session.StartNextGame(first.Id, new[] { original[0], original[2], original[1], original[3] }, true, session.UpdatedAt);

        session.StartReadyGames();
        var second = Current(session);
        session.FinishGame(second.Id, session.UpdatedAt.AddMinutes(10));
        var afterOverride = await Read(session);
        Assert.Equal(2, afterOverride.CompletedGames);
        Assert.Equal(8, afterOverride.PlayerAppearances);
        Assert.Equal(2, afterOverride.Players.Sum(p => p.Wins));
        Assert.Equal(2, afterOverride.Players.Sum(p => p.Losses));
        Assert.All(afterOverride.Players.Where(p => original.Contains(p.PlayerId)), p =>
        {
            Assert.Equal(2, p.GamesPlayed);
            Assert.Equal(2, p.DistinctTeammates);
            Assert.Equal(3, p.DistinctOpponents);
        });
        session.StartNextGame(second.Id, waiting, true, session.UpdatedAt);

        session.StartReadyGames();
        var third = Current(session);
        session.CorrectScore(third.Id, 8, 11, PlayTeam.B, 2, session.UpdatedAt.AddMinutes(10));
        session.End(session.UpdatedAt);
        var ended = await Read(session);
        Assert.Equal(3, ended.CompletedGames);
        Assert.Equal(12, ended.PlayerAppearances);
        Assert.Equal(2, ended.RecordedRallies); // No rallies fabricated by corrections or Finish.
        Assert.Equal(1, ended.TaggedRallies);
        Assert.Equal(4, ended.Players.Sum(p => p.Wins));
        Assert.Equal(4, ended.Players.Sum(p => p.Losses));
        Assert.All(ended.Players.Take(4), p => Assert.Equal(2, p.GamesPlayed));
        var expected = session.Players.OrderByDescending(p => original.Contains(p.Id) ? 2 : 1)
            .ThenByDescending(p => original.Take(2).Contains(p.Id) || waiting.Skip(2).Contains(p.Id) ? 1 : 0)
            .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase).Select(p => p.Id);
        Assert.Equal(expected, ended.Players.Select(p => p.PlayerId));
        Assert.Equal(3, session.Matches.Count);
        Assert.Equal(2, first.Rallies.Count);
    }

    [Fact]
    public async Task Queue_only_counts_games_and_variety_but_has_no_scoring_totals_or_results()
    {
        var session = Session(PlaySessionMode.QueueOnly);
        var first = Current(session);
        var players = Slots(first);
        session.FinishGame(first.Id, Now);
        session.StartNextGame(first.Id, players, true, Now);

        session.StartReadyGames();
        session.FinishGame(Current(session).Id, Now);
        var insights = await Read(session);
        Assert.Equal(2, insights.CompletedGames);
        Assert.Equal(8, insights.PlayerAppearances);
        Assert.Null(insights.RecordedRallies);
        Assert.Null(insights.TaggedRallies);
        Assert.All(insights.Players, p => { Assert.Equal(0, p.Wins); Assert.Equal(0, p.Losses); });
        Assert.All(insights.Players.Where(p => players.Contains(p.PlayerId)), p =>
        {
            Assert.Equal(2, p.GamesPlayed);
            Assert.Equal(1, p.DistinctTeammates);
            Assert.Equal(2, p.DistinctOpponents);
        });
        Assert.All(insights.Players.Where(p => !players.Contains(p.PlayerId)), p => Assert.Equal(0, p.GamesPlayed));
    }

    [Fact]
    public async Task Empty_draft_and_legacy_unrecorded_games_remain_valid()
    {
        var draft = new PlaySession(Guid.NewGuid(), "ABCDEF", "Empty", new(2026, 9, 15), new(18, 0), new(21, 0), 2, null, Now);
        var empty = await Read(draft);
        Assert.Equal(0, empty.TotalPlayers);
        Assert.Equal(2, empty.NumberOfCourts);
        Assert.Empty(empty.Players);
        var live = Session();
        live.FinishGame(Current(live).Id, Now);
        var unrecorded = await Read(live);
        Assert.Equal(1, unrecorded.CompletedGames);
        Assert.Equal(0, unrecorded.RecordedRallies);
        Assert.Equal(0, unrecorded.TaggedRallies);
        Assert.All(unrecorded.Players, p => { Assert.Equal(0, p.Wins); Assert.Equal(0, p.Losses); });
    }

    [Theory]
    [InlineData(PlayTeam.A)]
    [InlineData(PlayTeam.B)]
    [InlineData(null)]
    public async Task Queue_only_manual_result_flows_to_history_and_insights_without_scores(PlayTeam? winner)
    {
        var session = Session(PlaySessionMode.QueueOnly);
        var match = Current(session);
        session.FinishGame(match.Id, Now, winner);
        var detail = (await new PlaySessionService(new Store(session),
            new PlaySessionServiceTests.SequenceCodes("ABCDEF")).FindAsync("ABCDEF", default))!;
        var history = Assert.Single(detail.MatchHistory);
        Assert.Equal(winner, history.Winner);
        Assert.Null(history.TeamAScore);
        Assert.Null(history.TeamBScore);
        Assert.Null(history.TotalRallies);
        Assert.Null(history.TaggedRallies);
        Assert.Empty(history.Rallies);
        Assert.Equal(0, match.TeamAScore);
        Assert.Equal(0, match.TeamBScore);
        Assert.True(match.IsCurrent);
        Assert.Equal(4, detail.Insights.PlayerAppearances);
        Assert.Null(detail.Insights.RecordedRallies);
        foreach (var slot in match.Players)
        {
            var player = detail.Insights.Players.Single(p => p.PlayerId == slot.PlayerId);
            Assert.Equal(1, player.GamesPlayed);
            Assert.Equal(winner == slot.Team ? 1 : 0, player.Wins);
            Assert.Equal(winner.HasValue && winner != slot.Team ? 1 : 0, player.Losses);
        }
    }

    [Fact]
    public void Manual_result_is_rejected_for_live_scoring_and_invalid_teams()
    {
        var live = Session();
        var match = Current(live);
        Assert.Throws<PlayConflictException>(() => live.FinishGame(match.Id, Now, PlayTeam.A));
        Assert.Equal(PlayMatchStatus.Active, match.Status);
        live.FinishGame(match.Id, Now);
        Assert.Null(match.Winner);
        var queue = Session(PlaySessionMode.QueueOnly);
        Assert.Throws<ArgumentException>(() => queue.FinishGame(Current(queue).Id, Now, (PlayTeam)99));
        Assert.Equal(PlayMatchStatus.Active, Current(queue).Status);
    }

    private sealed class Store(PlaySession session) : IPlaySessionStore
    {
        public Task<PlaySession?> FindAsync(string code, CancellationToken cancellationToken) => Task.FromResult<PlaySession?>(session);
        public Task<bool> TryAddAsync(PlaySession value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlaySession?> UpdateAsync(string code, Action<PlaySession> update, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
