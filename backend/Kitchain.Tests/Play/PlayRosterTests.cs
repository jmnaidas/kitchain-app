using Kitchain.Application.Play;
using Kitchain.Domain.Play;

namespace Kitchain.Tests.Play;

public sealed class PlayRosterTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);
    private static PlaySession Session(int count = 12, PlaySessionMode mode = PlaySessionMode.QueueOnly)
    {
        var session = new PlaySession(Guid.NewGuid(), "ABCDEF", "Crew", new(2026, 9, 15), new(18, 0), new(21, 0), 1, null, Now, mode);
        for (var i = 0; i < count; i++) session.AddGuest($"Player {i}", Now);
        session.Start(Now);

        session.StartReadyGames();
        return session;
    }
    private static PlayMatch Current(PlaySession session) => session.Matches.Single(m => m.IsCurrent);
    private static async Task<PlaySessionDetail> Read(PlaySession session) => (await new PlaySessionService(
        new Store(session), new PlaySessionServiceTests.SequenceCodes("ABCDEF")).FindAsync("ABCDEF", default))!;

    [Theory]
    [InlineData(PlaySessionMode.QueueOnly)]
    [InlineData(PlaySessionMode.LiveScoring)]
    public async Task Rename_and_removal_preserve_completed_names_identity_results_and_insights(PlaySessionMode mode)
    {
        var session = Session(8, mode);
        var first = Current(session);
        var slot = first.Players.First();
        var player = session.Players.Single(p => p.Id == slot.PlayerId);
        session.RenameGuest(player.Id, "  On court name  ", Now);
        Assert.Equal("On court name", slot.DisplayName);
        if (mode == PlaySessionMode.QueueOnly) session.FinishGame(first.Id, Now, PlayTeam.A);
        else
        {
            session.RecordRally(first.Id, PlayTeam.A, Now);
            session.EditRallyCallOut(first.Id, first.Rallies.Single().Id, PlayRallyCallOut.Drive, null, Now);
            session.CorrectScore(first.Id, 11, 8, PlayTeam.A, 2, Now);
        }
        var before = (await Read(session)).MatchHistory.Single();
        var historyJson = System.Text.Json.JsonSerializer.Serialize(before);
        session.RenameGuest(player.Id, "Current name", Now);
        Assert.Throws<PlayConflictException>(() => session.RemoveGuest(player.Id, Now));
        session.StartNextGame(first.Id, session.NextLineup(first.Id).Select(p => p.Id).ToArray(), false, Now);

        session.StartReadyGames();
        Assert.Equal(PlayPlayerState.Waiting, player.State);
        var fairness = player.AdjustedGamesStarted;
        session.Rest(player.Id, Now);
        session.RemoveGuest(player.Id, Now);
        var after = await Read(session);
        Assert.DoesNotContain(after.Players, p => p.Id == player.Id);
        Assert.Equal(7, after.Insights.TotalPlayers);
        Assert.Equal(historyJson, System.Text.Json.JsonSerializer.Serialize(after.MatchHistory.Single()));
        var insight = after.Insights.Players.Single(p => p.PlayerId == player.Id);
        Assert.True(insight.IsRemoved);
        Assert.Equal(1, insight.GamesPlayed);
        Assert.Equal(slot.Team == PlayTeam.A ? 1 : 0, insight.Wins);
        Assert.Equal(slot.Team == PlayTeam.B ? 1 : 0, insight.Losses);
        Assert.Equal(1, insight.DistinctTeammates);
        Assert.Equal(2, insight.DistinctOpponents);
        Assert.Equal(fairness, player.AdjustedGamesStarted);
        Assert.Throws<KeyNotFoundException>(() => session.Rejoin(player.Id, Now));
        Assert.Throws<KeyNotFoundException>(() => session.RemoveGuest(player.Id, Now));
        session.AddGuest("Current name", Now); // A new identity never inherits the removed person's history.
        Assert.Equal(0, (await Read(session)).Insights.Players.Single(p => p.PlayerId != player.Id && p.DisplayName == "Current name").GamesPlayed);
    }

    [Fact]
    public async Task Sitting_out_rejoining_and_removal_recompute_preview_without_resetting_fairness()
    {
        var session = Session();
        var chosen = session.RotationPreview().Next.First();
        var identity = chosen.Id;
        session.Rest(identity, Now);
        Assert.DoesNotContain(session.RotationPreview().Next, p => p.Id == identity);
        Assert.DoesNotContain(session.WaitingQueue, p => p.Id == identity);
        for (var i = 0; i < 3; i++)
        {
            var game = Current(session);
            session.FinishGame(game.Id, Now);
            session.StartNextGame(game.Id, session.NextLineup(game.Id).Select(p => p.Id).ToArray(), false, Now);

            session.StartReadyGames();
        }
        var history = (await Read(session)).MatchHistory;
        var previous = chosen.AdjustedGamesStarted;
        var baseline = session.Players.Where(p => p.State != PlayPlayerState.Resting)
            .Select(p => p.AdjustedGamesStarted).Order().ToArray();
        var ticket = session.NextQueueOrder;
        session.Rejoin(identity, Now);
        Assert.Equal(Math.Max(previous, baseline[(baseline.Length - 1) / 2]), chosen.AdjustedGamesStarted);
        Assert.Equal(ticket + 1, chosen.QueueOrder);
        Assert.Equal(identity, chosen.Id);
        Assert.Equal(history.Count, (await Read(session)).MatchHistory.Count);
        Assert.All(session.RotationPreview().Next, p => Assert.Equal(PlayPlayerState.Waiting, p.State));
        foreach (var waiting in session.WaitingQueue.ToArray()) session.Rest(waiting.Id, Now);
        Assert.Empty(session.RotationPreview().Next);
        Assert.Empty(session.WaitingQueue);
        Assert.Equal(4, session.RotationPreview().Needed);
        var resting = session.Players.First(p => p.State == PlayPlayerState.Resting);
        session.RemoveGuest(resting.Id, Now);
        Assert.DoesNotContain((await Read(session)).Players, p => p.Id == resting.Id);
        var gameNow = Current(session);
        session.FinishGame(gameNow.Id, Now);
        Assert.Empty(session.RotationPreview().Next);
        Assert.Equal(4, session.RotationPreview().Held);
        Assert.Equal(4, session.NextLineup(gameNow.Id).Count);
    }

    [Fact]
    public async Task Preview_removal_and_rename_validation_are_safe_and_ended_sessions_reject_all_management()
    {
        var session = Session(9);
        var waiting = session.RotationPreview().Next.First();
        var playing = session.Players.First(p => p.State == PlayPlayerState.Playing);
        Assert.Throws<PlayConflictException>(() => session.Rest(playing.Id, Now));
        Assert.Throws<PlayConflictException>(() => session.RemoveGuest(playing.Id, Now));
        Assert.Throws<ArgumentException>(() => session.RenameGuest(waiting.Id, "  ", Now));
        Assert.Throws<PlayConflictException>(() => session.RenameGuest(waiting.Id, playing.DisplayName.ToUpperInvariant(), Now));
        var oldTicket = waiting.QueueOrder;
        session.RenameGuest(waiting.Id, "Renamed", Now);
        Assert.Equal(oldTicket, waiting.QueueOrder);
        session.RemoveGuest(waiting.Id, Now);
        Assert.Equal(4, session.RotationPreview().Next.Count);
        Assert.DoesNotContain(session.RotationPreview().Next, p => p.Id == waiting.Id);
        var remaining = session.WaitingQueue.First();
        session.Rest(remaining.Id, Now);
        Assert.Empty(session.RotationPreview().Next);
        Assert.Equal(1, session.RotationPreview().Needed);
        session.End(Now);
        Assert.Throws<PlayConflictException>(() => session.Rejoin(remaining.Id, Now));
        Assert.Throws<PlayConflictException>(() => session.Rest(playing.Id, Now));
        Assert.Throws<PlayConflictException>(() => session.RenameGuest(remaining.Id, "Late", Now));
        Assert.Throws<PlayConflictException>(() => session.RemoveGuest(remaining.Id, Now));
        Assert.Empty((await Read(session)).Queue.NextUp);
    }

    [Fact]
    public void Multi_court_preview_uses_oldest_completed_court_and_never_changes_fairness_state()
    {
        var session = new PlaySession(Guid.NewGuid(), "ABCDEF", "Crew", new(2026, 9, 15), new(18, 0), new(21, 0), 2, null, Now);
        for (var i = 0; i < 16; i++) session.AddGuest($"Player {i}", Now);
        session.Start(Now);

        session.StartReadyGames();
        var courtTwo = session.Matches.Single(m => m.CourtNumber == 2);
        session.FinishGame(courtTwo.Id, Now);
        session.FinishGame(session.Matches.Single(m => m.CourtNumber == 1).Id, Now.AddMinutes(1));
        var before = session.Players.Select(p => (p.Id, p.State, p.QueueOrder, p.AdjustedGamesStarted, p.MissedOpportunities)).ToArray();
        var expected = session.NextLineup(courtTwo.Id).Where(p => p.State == PlayPlayerState.Waiting).Select(p => p.Id).ToArray();
        var preview = session.RotationPreview();
        Assert.Equal(2, preview.Court);
        Assert.Equal(expected, preview.Next.Select(p => p.Id));
        Assert.DoesNotContain(preview.Next, p => p.State != PlayPlayerState.Waiting);
        Assert.Equal(preview.Next.Select(p => p.Id), session.RotationPreview().Next.Select(p => p.Id));
        Assert.Equal(before, session.Players.Select(p => (p.Id, p.State, p.QueueOrder, p.AdjustedGamesStarted, p.MissedOpportunities)));
    }

    private sealed class Store(PlaySession session) : IPlaySessionStore
    {
        public Task<PlaySession?> FindAsync(string code, CancellationToken cancellationToken) => Task.FromResult<PlaySession?>(session);
        public Task<bool> TryAddAsync(PlaySession value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlaySession?> UpdateAsync(string code, Action<PlaySession> update, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
