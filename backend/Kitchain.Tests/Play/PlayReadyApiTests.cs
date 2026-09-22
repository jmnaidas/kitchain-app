using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kitchain.Application.Play;
using Kitchain.Domain.Play;
using Kitchain.Infrastructure.Play;
using Kitchain.Tests.Courts;
using Microsoft.EntityFrameworkCore;

namespace Kitchain.Tests.Play;

public sealed class PlayReadyApiTests(PostgresCourtFixture fixture) : IClassFixture<PostgresCourtFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter() } };
    private async Task<(string Url, PlaySessionDetail State)> Create(PlaySessionMode mode = PlaySessionMode.LiveScoring)
    {
        using var response = await fixture.Client.PostAsJsonAsync("/api/play/sessions",
            PlaySessionServiceTests.Input with { NumberOfCourts = 2, Mode = mode }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var draft = (await response.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!;
        var url = $"/api/play/sessions/{draft.JoinCode}";
        for (var i = 0; i < 11; i++)
            (await fixture.Client.PostAsJsonAsync($"{url}/players", new { displayName = $"Ready player {i}" })).EnsureSuccessStatusCode();
        (await fixture.Client.PostAsync($"{url}/start", null)).EnsureSuccessStatusCode();
        return (url, await Read(url));
    }
    private async Task<PlaySessionDetail> Read(string url) => (await fixture.Client.GetFromJsonAsync<PlaySessionDetail>(url, Json))!;
    private Task<HttpResponseMessage> Swap(string url, Guid match, int position, Guid player, long revision) =>
        fixture.Client.PatchAsJsonAsync($"{url}/matches/{match}/lineup", new { position, playerId = player, expectedRevision = revision });
    private Task<HttpResponseMessage> Start(string url, PlayMatchDetail match) =>
        fixture.Client.PostAsJsonAsync($"{url}/matches/{match.Id}/start", new { expectedRevision = match.LineupRevision });

    [PostgresTheory]
    [InlineData(PlaySessionMode.LiveScoring)]
    [InlineData(PlaySessionMode.QueueOnly)]
    public async Task Slots_swap_replace_reset_and_start_persist_without_rewriting_other_court_or_history(PlaySessionMode mode)
    {
        var (url, state) = await Create(mode);
        Assert.Empty(state.ActiveMatches); Assert.Empty(state.MatchHistory);
        Assert.All(state.CurrentMatches, m => { Assert.Equal(PlayMatchStatus.Ready, m.Status); Assert.Null(m.StartedAt); });
        var first = state.CurrentMatches[0]; var second = state.CurrentMatches[1];
        var original = first.Players.Select(p => p.PlayerId).ToArray();
        var tickets = state.Players.ToDictionary(p => p.Id, p => p.QueueOrder);
        Assert.Equal(HttpStatusCode.OK, (await Swap(url, first.Id, 2, original[2], 0)).StatusCode);
        var swapped = (await Read(url)).CurrentMatches[0];
        Assert.Equal(new[] { original[0], original[2], original[1], original[3] }, swapped.Players.Select(p => p.PlayerId));
        var waiting = state.WaitingQueue[0];
        Assert.Equal(HttpStatusCode.OK, (await Swap(url, first.Id, 2, waiting.Id, 1)).StatusCode);
        state = await Read(url);
        Assert.Contains(state.WaitingQueue, p => p.Id == original[2]);
        Assert.DoesNotContain(state.WaitingQueue, p => p.Id == waiting.Id);
        Assert.All(state.Players, p => Assert.Equal(tickets[p.Id], p.QueueOrder));
        Assert.Equal(second.Players, state.CurrentMatches[1].Players);
        Assert.Equal(HttpStatusCode.Conflict, (await Start(url, first)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.PostAsJsonAsync($"{url}/matches/{first.Id}/lineup/reset", new { expectedRevision = 2 })).StatusCode);
        state = await Read(url); first = state.CurrentMatches[0];
        Assert.False(first.IsLineupOverridden); Assert.Equal(3, first.LineupRevision);
        Assert.Equal(HttpStatusCode.OK, (await Start(url, first)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Start(url, first)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Swap(url, first.Id, 1, waiting.Id, 3)).StatusCode);
        state = await Read(url);
        Assert.Equal(PlayMatchStatus.Active, state.CurrentMatches[0].Status);
        Assert.Equal(PlayMatchStatus.Ready, state.CurrentMatches[1].Status);
        Assert.Equal(2, state.CurrentMatches[0].CurrentServerNumber);
        if (mode == PlaySessionMode.LiveScoring)
            Assert.Equal(HttpStatusCode.OK, (await fixture.Client.PostAsJsonAsync($"{url}/matches/{first.Id}/rallies", new { winner = "A" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.PostAsync($"{url}/matches/{first.Id}/finish", null)).StatusCode);
        state = await Read(url); var held = state.CurrentMatches[0];
        Assert.Equal(HttpStatusCode.Conflict, (await Swap(url, first.Id, 1, waiting.Id, 3)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.PostAsJsonAsync($"{url}/matches/{first.Id}/next",
            new { playerIds = held.NextLineup.Select(p => p.PlayerId), overrideLineup = false })).StatusCode);
        state = await Read(url);
        Assert.All(state.CurrentMatches, m => Assert.Equal(PlayMatchStatus.Ready, m.Status));
        Assert.Equal(first.Players, Assert.Single(state.MatchHistory).Players);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.PostAsync($"{url}/end", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Start(url, state.CurrentMatches[0])).StatusCode);
    }

    [PostgresFact]
    public async Task Invalid_edits_leave_slots_queue_revision_and_fairness_unchanged()
    {
        var (url, state) = await Create(); var match = state.CurrentMatches[0];
        var resting = state.WaitingQueue[0];
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.PostAsync($"{url}/players/{resting.Id}/rest", null)).StatusCode);
        var before = await Read(url);
        foreach (var id in new[] { resting.Id, state.CurrentMatches[1].Players[0].PlayerId, Guid.NewGuid() })
            Assert.Equal(HttpStatusCode.Conflict, (await Swap(url, match.Id, 1, id, 0)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Swap(url, match.Id, 0, match.Players[0].PlayerId, 0)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Swap(url, Guid.NewGuid(), 1, match.Players[0].PlayerId, 0)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Swap("/api/play/sessions/ZZZZZZ", match.Id, 1, match.Players[0].PlayerId, 0)).StatusCode);
        var after = await Read(url);
        Assert.Equal(JsonSerializer.Serialize(before, Json), JsonSerializer.Serialize(after, Json));
        await using var db = fixture.CreateDbContext();
        Assert.All(await db.Set<PlaySessionPlayer>().Where(p => p.SessionId == state.Id).ToListAsync(), p => Assert.Equal(0, p.AdjustedGamesStarted));
    }

    [PostgresFact]
    public async Task Edit_racing_start_accepts_only_one_intent_and_preserves_final_lineup()
    {
        var (url, state) = await Create(); var match = state.CurrentMatches[0];
        var results = await Task.WhenAll(Swap(url, match.Id, 1, state.WaitingQueue[0].Id, 0), Start(url, match));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        var saved = await Read(url); var current = saved.CurrentMatches[0];
        Assert.Equal(8, saved.CurrentMatches.SelectMany(m => m.Players).Select(p => p.PlayerId).Distinct().Count());
        if (current.Status == PlayMatchStatus.Ready)
        {
            Assert.Equal(1, current.LineupRevision);
            Assert.Equal(state.WaitingQueue[0].Id, current.Players[0].PlayerId);
            Assert.Equal(HttpStatusCode.OK, (await Start(url, current)).StatusCode);
        }
        else Assert.Equal(match.Players, current.Players);
        await using var db = fixture.CreateDbContext();
        var aggregate = (await new EfPlaySessionStore(db).FindAsync(state.JoinCode, default))!;
        var finalIds = aggregate.Matches.Single(m => m.Id == match.Id).Players.Select(p => p.PlayerId).ToHashSet();
        Assert.All(aggregate.Players, p => Assert.Equal(finalIds.Contains(p.Id) ? 1 : 0, p.AdjustedGamesStarted));
    }
}
