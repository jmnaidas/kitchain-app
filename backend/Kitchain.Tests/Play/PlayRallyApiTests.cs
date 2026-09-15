using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kitchain.Application.Play;
using Kitchain.Domain.Play;
using Kitchain.Tests.Courts;
using Microsoft.EntityFrameworkCore;

namespace Kitchain.Tests.Play;

public sealed class PlayRallyApiTests(PostgresCourtFixture fixture) : IClassFixture<PostgresCourtFixture>
{
    private const string Route = "/api/play/sessions";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
    private async Task<PlaySessionDetail> Start(PlaySessionMode mode = PlaySessionMode.LiveScoring)
    {
        using var created = await fixture.Client.PostAsJsonAsync(Route, PlaySessionServiceTests.Input with { NumberOfCourts = 1, Mode = mode }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var draft = (await created.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!;
        var url = $"{Route}/{draft.JoinCode}";
        for (var i = 0; i < 8; i++)
        {
            using var added = await fixture.Client.PostAsJsonAsync($"{url}/players", new { displayName = $"Rally player {i}" });
            Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        }
        using var started = await fixture.Client.PostAsync($"{url}/start", null);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        return (await started.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!;
    }

    [PostgresFact]
    public async Task Rally_snapshots_and_metadata_round_trip_while_corrections_and_next_game_preserve_history()
    {
        var session = await Start();
        var url = $"{Route}/{session.JoinCode}";
        var matchId = Assert.Single(session.CurrentMatches).Id;
        var matchUrl = $"{url}/matches/{matchId}";
        using var scored = await fixture.Client.PostAsJsonAsync($"{matchUrl}/rallies", new { winner = "B" });
        Assert.Equal(HttpStatusCode.OK, scored.StatusCode);
        var state = (await scored.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!;
        var match = Assert.Single(state.CurrentMatches);
        var first = Assert.Single(match.Rallies);
        Assert.Equal((PlayTeam.B, false, 0, 0, PlayTeam.B, 1),
            (first.Winner, first.PointAwarded, first.TeamAScore, first.TeamBScore, first.ServingTeam, first.CurrentServerNumber));
        Assert.Equal(1, first.Sequence);
        Assert.Null(first.CallOut);
        var tagUrl = $"{matchUrl}/rallies/{first.Id}/call-out";
        using var tagged = await fixture.Client.PatchAsJsonAsync(tagUrl, new EditPlayRallyCallOut { CallOut = PlayRallyCallOut.Drive }, Json);
        Assert.Equal(HttpStatusCode.OK, tagged.StatusCode);
        using var stale = await fixture.Client.PatchAsJsonAsync(tagUrl, new EditPlayRallyCallOut { CallOut = PlayRallyCallOut.Out }, Json);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var invalid = await fixture.Client.PatchAsJsonAsync(tagUrl, new { callOut = "Smash", expectedCallOut = "Drive" });
        using var missing = await fixture.Client.PatchAsJsonAsync(tagUrl, new { expectedCallOut = "Drive" });
        using var rewriteWinner = await fixture.Client.PatchAsJsonAsync(tagUrl, new { callOut = "Out", expectedCallOut = "Drive", winner = "A" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, rewriteWinner.StatusCode);
        using var changed = await fixture.Client.PatchAsJsonAsync(tagUrl, new EditPlayRallyCallOut { CallOut = PlayRallyCallOut.Kitchen, ExpectedCallOut = PlayRallyCallOut.Drive }, Json);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        state = (await fixture.Client.GetFromJsonAsync<PlaySessionDetail>(url, Json))!;
        Assert.Equal(PlayRallyCallOut.Kitchen, Assert.Single(state.CurrentMatches[0].Rallies).CallOut);
        using var removed = await fixture.Client.PatchAsJsonAsync(tagUrl, new EditPlayRallyCallOut { ExpectedCallOut = PlayRallyCallOut.Kitchen }, Json);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        using var corrected = await fixture.Client.PatchAsJsonAsync($"{matchUrl}/score", new { teamAScore = 10, teamBScore = 9, servingTeam = "A", currentServerNumber = 2 });
        Assert.Equal(HttpStatusCode.OK, corrected.StatusCode);
        state = (await corrected.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!;
        var historical = Assert.Single(state.CurrentMatches[0].Rallies);
        // PostgreSQL stores microseconds; the initial response can contain finer UTC ticks.
        Assert.Equal(first with { CreatedAt = historical.CreatedAt }, historical);
        Assert.InRange((historical.CreatedAt - first.CreatedAt).Duration(), TimeSpan.Zero, TimeSpan.FromTicks(10));
        using var won = await fixture.Client.PostAsJsonAsync($"{matchUrl}/rallies", new { winner = "A" });
        Assert.Equal(HttpStatusCode.OK, won.StatusCode);
        state = (await won.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!;
        match = state.CurrentMatches[0];
        Assert.Equal(PlayMatchStatus.Completed, match.Status);
        Assert.Equal(new long[] { 1, 2 }, match.Rallies.Select(r => r.Sequence));
        var last = match.Rallies[1];
        Assert.True(last.PointAwarded);
        Assert.Equal(11, last.TeamAScore);
        using var finalTag = await fixture.Client.PatchAsJsonAsync($"{matchUrl}/rallies/{last.Id}/call-out", new EditPlayRallyCallOut { CallOut = PlayRallyCallOut.Dink }, Json);
        Assert.Equal(HttpStatusCode.OK, finalTag.StatusCode);
        using var lateRally = await fixture.Client.PostAsJsonAsync($"{matchUrl}/rallies", new { winner = "A" });
        Assert.Equal(HttpStatusCode.Conflict, lateRally.StatusCode);
        using var next = await fixture.Client.PostAsJsonAsync($"{matchUrl}/next", new { playerIds = match.NextLineup.Select(p => p.PlayerId), overrideLineup = false });
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        state = (await next.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!;
        Assert.NotEqual(matchId, state.CurrentMatches[0].Id);
        Assert.Empty(state.CurrentMatches[0].Rallies);
        var summary = Assert.Single(state.MatchHistory);
        Assert.Equal(matchId, summary.Id);
        Assert.Equal(11, summary.TeamAScore);
        Assert.Equal(9, summary.TeamBScore);
        Assert.Equal(PlayTeam.A, summary.Winner);
        Assert.Equal(2, summary.TotalRallies);
        Assert.Equal(1, summary.TaggedRallies);
        Assert.Equal(new PlayCallOutCount(PlayRallyCallOut.Dink, 1), Assert.Single(summary.CallOutCounts));
        using var historicalEdit = await fixture.Client.PatchAsJsonAsync(tagUrl, new EditPlayRallyCallOut { CallOut = PlayRallyCallOut.Out }, Json);
        Assert.Equal(HttpStatusCode.Conflict, historicalEdit.StatusCode);
        using var wrongMatch = await fixture.Client.PatchAsJsonAsync($"{url}/matches/{state.CurrentMatches[0].Id}/rallies/{first.Id}/call-out", new EditPlayRallyCallOut { CallOut = PlayRallyCallOut.Out }, Json);
        Assert.Equal(HttpStatusCode.NotFound, wrongMatch.StatusCode);
        await using var db = fixture.CreateDbContext();
        var stored = await db.Set<PlayRallyEvent>().Where(r => r.MatchId == matchId).OrderBy(r => r.Sequence).ToListAsync();
        Assert.Equal(2, stored.Count);
        Assert.Null(stored[0].CallOut);
        Assert.Equal(PlayRallyCallOut.Dink, stored[1].CallOut);
        Assert.Equal(first.Id, stored[0].Id);
        Assert.Equal(0, stored[0].TeamAScore);
        Assert.Empty(await db.Set<PlayRallyEvent>().Where(r => r.MatchId == state.CurrentMatches[0].Id).ToListAsync());
        using var ended = await fixture.Client.PostAsync($"{url}/end", null);
        Assert.Equal(HttpStatusCode.OK, ended.StatusCode);
        var reloaded = (await fixture.Client.GetFromJsonAsync<PlaySessionDetail>(url, Json))!;
        Assert.Equal(PlaySessionStatus.Ended, reloaded.Status);
        Assert.Equal(summary.Rallies, Assert.Single(reloaded.MatchHistory).Rallies);
        using var endedTag = await fixture.Client.PatchAsJsonAsync(tagUrl, new EditPlayRallyCallOut { CallOut = PlayRallyCallOut.Out }, Json);
        Assert.Equal(HttpStatusCode.Conflict, endedTag.StatusCode);
    }

    [PostgresFact]
    public async Task Concurrent_accepted_rallies_each_have_one_event_and_concurrent_stale_tags_conflict()
    {
        var session = await Start();
        var url = $"{Route}/{session.JoinCode}";
        var matchUrl = $"{url}/matches/{session.CurrentMatches[0].Id}";
        // Identical requests may represent separate legitimate rallies: no timing-based deduplication.
        var responses = await Task.WhenAll(Enumerable.Range(0, 2)
            .Select(_ => fixture.Client.PostAsJsonAsync($"{matchUrl}/rallies", new { winner = "B" })));
        try { Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode)); }
        finally { foreach (var response in responses) response.Dispose(); }
        var state = (await fixture.Client.GetFromJsonAsync<PlaySessionDetail>(url, Json))!;
        var match = state.CurrentMatches[0];
        Assert.Equal(new long[] { 1, 2 }, match.Rallies.Select(r => r.Sequence));
        Assert.Equal(2, match.Rallies.Select(r => r.Id).Distinct().Count());
        Assert.False(match.Rallies[0].PointAwarded);
        Assert.True(match.Rallies[1].PointAwarded);
        Assert.Equal(1, match.TeamBScore);
        var tagUrl = $"{matchUrl}/rallies/{match.Rallies[1].Id}/call-out";
        var edits = await Task.WhenAll(new[] { PlayRallyCallOut.Drive, PlayRallyCallOut.Out }
            .Select(tag => fixture.Client.PatchAsJsonAsync(tagUrl, new EditPlayRallyCallOut { CallOut = tag }, Json)));
        try
        {
            Assert.Single(edits, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(edits, response => response.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var response in edits) response.Dispose(); }
        await using var db = fixture.CreateDbContext();
        Assert.Equal(2, await db.Set<PlayRallyEvent>().CountAsync(r => r.MatchId == match.Id));
        var saved = await db.Set<PlayMatch>().SingleAsync(m => m.Id == match.Id);
        Assert.Equal(1, saved.TeamBScore);
    }

    [PostgresFact]
    public async Task Queue_only_rejects_scoring_metadata_and_finishes_without_events()
    {
        var session = await Start(PlaySessionMode.QueueOnly);
        var match = session.CurrentMatches[0];
        var url = $"{Route}/{session.JoinCode}/matches/{match.Id}";
        using var rally = await fixture.Client.PostAsJsonAsync($"{url}/rallies", new { winner = "A" });
        using var tag = await fixture.Client.PatchAsJsonAsync($"{url}/rallies/{Guid.NewGuid()}/call-out", new EditPlayRallyCallOut { CallOut = PlayRallyCallOut.Fault }, Json);
        Assert.Equal(HttpStatusCode.Conflict, rally.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, tag.StatusCode);
        using var finish = await fixture.Client.PostAsync($"{url}/finish", null);
        Assert.Equal(HttpStatusCode.OK, finish.StatusCode);
        var held = (await finish.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!.CurrentMatches[0];
        Assert.Equal(PlayMatchStatus.Completed, held.Status);
        Assert.Empty(held.Rallies);
        Assert.Equal(4, held.NextLineup.Count);
        await using var db = fixture.CreateDbContext();
        Assert.Empty(await db.Set<PlayRallyEvent>().Where(r => r.MatchId == match.Id).ToListAsync());
    }
}
