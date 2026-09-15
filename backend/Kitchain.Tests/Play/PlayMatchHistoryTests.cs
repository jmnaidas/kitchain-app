using Kitchain.Application.Play;
using Kitchain.Domain.Play;

namespace Kitchain.Tests.Play;

public sealed class PlayMatchHistoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);
    private static PlaySession Session(PlaySessionMode mode = PlaySessionMode.LiveScoring)
    {
        var session = new PlaySession(Guid.NewGuid(), "ABCDEF", "Crew", new(2026, 9, 15), new(18, 0), new(21, 0), 2, null, Now, mode);
        for (var i = 0; i < 12; i++) session.AddGuest($"Player {i}", Now);
        session.Start(Now);
        return session;
    }
    private static PlayMatch Court(PlaySession session, int court = 1) => session.Matches.Single(m => m.IsCurrent && m.CourtNumber == court);
    private static PlaySessionService Service(PlaySession session) => new(new Store(session), new PlaySessionServiceTests.SequenceCodes("ABCDEF"));

    [Fact]
    public async Task Completed_summary_uses_final_state_and_only_recorded_rallies_and_tags()
    {
        var session = Session();
        var match = Court(session);
        var service = Service(session);
        Assert.Empty((await service.FindAsync("ABCDEF", default))!.MatchHistory);
        foreach (var tag in new PlayRallyCallOut?[] { PlayRallyCallOut.Drive, null, PlayRallyCallOut.Drive, PlayRallyCallOut.Dink })
        {
            session.RecordRally(match.Id, PlayTeam.A, Now);
            if (tag.HasValue) session.EditRallyCallOut(match.Id, match.Rallies.Last().Id, tag, null, Now);
        }
        session.CorrectScore(match.Id, 11, 8, PlayTeam.A, 2, Now.AddMinutes(20));
        var history = (await service.FindAsync("ABCDEF", default))!.MatchHistory;
        var summary = Assert.Single(history);
        Assert.Equal(match.Id, summary.Id);
        Assert.Equal(1, summary.CourtNumber);
        Assert.Equal(PlayTeam.A, summary.Winner);
        Assert.Equal(11, summary.TeamAScore);
        Assert.Equal(8, summary.TeamBScore);
        Assert.Equal(Now, summary.StartedAt);
        Assert.Equal(Now.AddMinutes(20), summary.CompletedAt);
        Assert.Equal(4, summary.TotalRallies);
        Assert.Equal(3, summary.TaggedRallies);
        Assert.Equal(new[] { new PlayCallOutCount(PlayRallyCallOut.Drive, 2), new PlayCallOutCount(PlayRallyCallOut.Dink, 1) }, summary.CallOutCounts);
        Assert.Equal(match.Players.OrderBy(p => p.Position).Select(p => p.PlayerId), summary.Players.Select(p => p.PlayerId));
        Assert.Equal(2, summary.Players.Count(p => p.Team == PlayTeam.A));
        Assert.Equal(new long[] { 1, 2, 3, 4 }, summary.Rallies.Select(r => r.Sequence));
        var tagged = match.Rallies.First();
        var proposal = session.NextLineup(match.Id).Select(p => p.Id).ToArray();
        session.EditRallyCallOut(match.Id, tagged.Id, null, PlayRallyCallOut.Drive, session.UpdatedAt);
        var updated = Assert.Single((await service.FindAsync("ABCDEF", default))!.MatchHistory);
        Assert.Equal(2, updated.TaggedRallies);
        Assert.Equal(1, updated.CallOutCounts.Single(c => c.CallOut == PlayRallyCallOut.Drive).Count);
        Assert.Equal(proposal, session.NextLineup(match.Id).Select(p => p.Id));
    }

    [Fact]
    public async Task Queue_only_does_not_invent_zero_scores_winners_or_rally_counts()
    {
        var session = Session(PlaySessionMode.QueueOnly);
        session.FinishGame(Court(session).Id, Now);
        var summary = Assert.Single((await Service(session).FindAsync("ABCDEF", default))!.MatchHistory);
        Assert.Null(summary.TeamAScore);
        Assert.Null(summary.TeamBScore);
        Assert.Null(summary.Winner);
        Assert.Null(summary.TotalRallies);
        Assert.Null(summary.TaggedRallies);
        Assert.Empty(summary.Rallies);
        Assert.Empty(summary.CallOutCounts);
        Assert.Equal(4, summary.Players.Count);
    }

    [Fact]
    public async Task Next_game_and_ended_session_preserve_ordered_history_without_mutations()
    {
        var session = Session();
        var first = Court(session);
        var other = Court(session, 2);
        session.RecordRally(first.Id, PlayTeam.B, Now);
        session.FinishGame(first.Id, Now.AddMinutes(10));
        var firstBefore = Assert.Single((await Service(session).FindAsync("ABCDEF", default))!.MatchHistory);
        Assert.Null(firstBefore.Winner); // Finish override does not infer a winner.
        session.StartNextGame(first.Id, session.NextLineup(first.Id).Select(p => p.Id).ToArray(), false, session.UpdatedAt);
        Assert.Empty(Court(session).Rallies);
        session.FinishGame(other.Id, Now.AddMinutes(12));
        var service = Service(session);
        var detail = (await service.FindAsync("ABCDEF", default))!;
        Assert.Equal(new[] { other.Id, first.Id }, detail.MatchHistory.Select(m => m.Id));
        Assert.Equal(firstBefore.Rallies, detail.MatchHistory[1].Rallies);
        Assert.Equal(firstBefore.Players, detail.MatchHistory[1].Players);
        Assert.Equal(firstBefore.CompletedAt, detail.MatchHistory[1].CompletedAt);
        session.End(session.UpdatedAt);
        var ended = (await service.FindAsync("ABCDEF", default))!;
        Assert.Equal(new[] { other.Id, first.Id }, ended.MatchHistory.Select(m => m.Id));
        var current = Court(session);
        Assert.Throws<PlayConflictException>(() => session.RecordRally(current.Id, PlayTeam.A, session.UpdatedAt));
        Assert.Throws<PlayConflictException>(() => session.CorrectScore(current.Id, 1, 0, PlayTeam.A, 2, session.UpdatedAt));
        Assert.Throws<PlayConflictException>(() => session.FinishGame(current.Id, session.UpdatedAt));
        Assert.Throws<PlayConflictException>(() => session.StartNextGame(other.Id, [], true, session.UpdatedAt));
        Assert.Throws<PlayConflictException>(() => session.EditRallyCallOut(first.Id, first.Rallies.First().Id, PlayRallyCallOut.Out, null, session.UpdatedAt));
        Assert.Throws<PlayConflictException>(() => session.AddGuest("Late", session.UpdatedAt));
        Assert.Equal(1, first.Rallies.Count);
        Assert.Equal(3, session.Matches.Count);
    }

    [Fact]
    public async Task Equal_completion_times_have_a_stable_tie_break()
    {
        var session = Session();
        foreach (var match in session.Matches) session.FinishGame(match.Id, Now);
        var detail = (await Service(session).FindAsync("ABCDEF", default))!;
        Assert.Equal(session.Matches.OrderByDescending(m => m.Id).Select(m => m.Id), detail.MatchHistory.Select(m => m.Id));
    }

    private sealed class Store(PlaySession session) : IPlaySessionStore
    {
        public Task<PlaySession?> FindAsync(string code, CancellationToken cancellationToken) => Task.FromResult<PlaySession?>(session);
        public Task<bool> TryAddAsync(PlaySession value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlaySession?> UpdateAsync(string code, Action<PlaySession> update, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
