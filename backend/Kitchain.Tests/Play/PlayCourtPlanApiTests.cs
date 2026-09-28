using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kitchain.Application.Play;
using Kitchain.Domain.Play;
using Kitchain.Tests.Courts;

namespace Kitchain.Tests.Play;

public sealed class PlayCourtPlanApiTests(PostgresCourtFixture fixture) : IClassFixture<PostgresCourtFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private async Task<PlaySessionDetail> Read(string url) => (await fixture.Client.GetFromJsonAsync<PlaySessionDetail>(url, Json))!;
    private async Task<string> Create()
    {
        var response = await fixture.Client.PostAsJsonAsync("/api/play/sessions", PlaySessionServiceTests.Input with { NumberOfCourts = 2 }, Json);
        response.EnsureSuccessStatusCode();
        var session = (await response.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!;
        var url = "/api/play/sessions/" + session.JoinCode;
        for (var i = 1; i <= 16; i++)
            (await fixture.Client.PostAsJsonAsync(url + "/players", new { displayName = "Player " + i })).EnsureSuccessStatusCode();
        await Arrange(url, 1, 1, 2, 3, 4); await Arrange(url, 2, 5, 6, 7, 8);
        (await fixture.Client.PostAsync(url + "/start", null)).EnsureSuccessStatusCode();
        await fixture.Client.StartReadyGames(url);
        await Arrange(url, 1, 9, 10, 11, 12); await Arrange(url, 2, 13, 14, 15, 16);
        return url;
    }
    private async Task Arrange(string url, int court, params int[] numbers)
    {
        for (var i = 0; i < numbers.Length; i++)
        {
            var state = await Read(url); var plan = state.NextRound!;
            (await fixture.Client.PatchAsJsonAsync(url + "/next-round", new { matchId = plan.Courts.Single(c => c.CourtNumber == court).MatchId,
                position = i + 1, playerId = state.Players.Single(p => p.DisplayName == "Player " + numbers[i]).Id, expectedRevision = plan.Revision })).EnsureSuccessStatusCode();
        }
    }
    private async Task Finalize(string url, int court)
    {
        var plan = (await Read(url)).NextRound!;
        (await fixture.Client.PostAsJsonAsync(url + "/next-round/finalize", new { matchId = plan.Courts.Single(c => c.CourtNumber == court).MatchId, expectedRevision = plan.Revision })).EnsureSuccessStatusCode();
    }

    [PostgresFact]
    public async Task Independent_court_cycle_and_planning_survive_reload_without_changing_the_other_court()
    {
        var url = await Create(); await Finalize(url, 1); var state = await Read(url);
        var first = state.NextRound!.Courts[0]; var second = state.NextRound.Courts[1];
        Assert.True(first.Finalized); Assert.False(second.Finalized);
        var playing = JsonSerializer.Serialize(state.CurrentMatches[1], Json);
        (await fixture.Client.PostAsync($"{url}/matches/{first.MatchId}/finish", null)).EnsureSuccessStatusCode();
        state = await Read(url);
        Assert.Equal(first.MatchId, Assert.Single(state.MatchHistory).Id);
        Assert.Equal(PlayMatchStatus.Active, state.CurrentMatches[1].Status);
        var ids = first.Players.Select(p => p.PlayerId).ToArray();
        (await fixture.Client.PostAsJsonAsync($"{url}/matches/{first.MatchId}/next", new { playerIds = ids, overrideLineup = false })).EnsureSuccessStatusCode();
        state = await Read(url); var ready = state.CurrentMatches[0];
        Assert.Equal(PlayMatchStatus.Ready, ready.Status); Assert.Equal(ids, ready.Players.Select(p => p.PlayerId));
        Assert.Equal(playing, JsonSerializer.Serialize(state.CurrentMatches[1], Json));
        (await fixture.Client.PostAsJsonAsync($"{url}/matches/{ready.Id}/start", new { expectedRevision = ready.LineupRevision })).EnsureSuccessStatusCode();
        state = await Read(url);
        Assert.Equal(ready.Id, state.NextRound!.Courts[0].MatchId);
        Assert.Equal(JsonSerializer.Serialize(second, Json), JsonSerializer.Serialize(state.NextRound.Courts[1], Json));
        await Arrange(url, 1, 1, 2, 3, 4); await Finalize(url, 1);
        state = await Read(url); Assert.True(state.NextRound!.Courts[0].Finalized);
        Assert.Equal(playing, JsonSerializer.Serialize(state.CurrentMatches[1], Json));
    }

    [PostgresFact]
    public async Task Concurrent_intents_conflict_and_targeted_reset_preserves_the_other_finalized_plan()
    {
        var url = await Create(); var state = await Read(url); var plan = state.NextRound!;
        var responses = await Task.WhenAll(plan.Courts.Select(c => fixture.Client.PostAsJsonAsync(url + "/next-round/finalize",
            new { matchId = c.MatchId, expectedRevision = plan.Revision })));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        state = await Read(url); var saved = Assert.Single(state.NextRound!.Courts, c => c.Finalized);
        var other = state.NextRound.Courts.Single(c => !c.Finalized);
        (await fixture.Client.PostAsJsonAsync(url + "/next-round/reset", new { matchId = other.MatchId, expectedRevision = state.NextRound.Revision })).EnsureSuccessStatusCode();
        var reset = await Read(url);
        Assert.Equal(JsonSerializer.Serialize(saved, Json), JsonSerializer.Serialize(reset.NextRound!.Courts.Single(c => c.MatchId == saved.MatchId), Json));
        Assert.Equal(8, reset.NextRound.Courts.SelectMany(c => c.Players).Select(p => p.PlayerId).Distinct().Count());
    }

    [PostgresFact]
    public async Task Dependency_rejection_is_atomic_and_clears_after_the_other_court_proceeds()
    {
        var url = await Create(); await Arrange(url, 1, 9, 10, 11, 5); await Finalize(url, 1);
        var state = await Read(url); var first = state.NextRound!.Courts[0]; var second = state.NextRound.Courts[1];
        Assert.Equal(new[] { 2 }, first.WaitingForCourts);
        (await fixture.Client.PostAsync($"{url}/matches/{first.MatchId}/finish", null)).EnsureSuccessStatusCode();
        var held = await Read(url); var ids = first.Players.Select(p => p.PlayerId).ToArray();
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.Client.PostAsJsonAsync($"{url}/matches/{first.MatchId}/next", new { playerIds = ids, overrideLineup = false })).StatusCode);
        Assert.Equal(JsonSerializer.Serialize(held, Json), JsonSerializer.Serialize(await Read(url), Json));
        (await fixture.Client.PostAsync($"{url}/matches/{second.MatchId}/finish", null)).EnsureSuccessStatusCode();
        state = await Read(url);
        (await fixture.Client.PostAsJsonAsync($"{url}/matches/{second.MatchId}/next", new { playerIds = state.CurrentMatches[1].NextLineup.Select(p => p.PlayerId), overrideLineup = false })).EnsureSuccessStatusCode();
        (await fixture.Client.PostAsJsonAsync($"{url}/matches/{first.MatchId}/next", new { playerIds = ids, overrideLineup = false })).EnsureSuccessStatusCode();
        state = await Read(url);
        Assert.Equal(8, state.CurrentMatches.SelectMany(m => m.Players).Select(p => p.PlayerId).Distinct().Count());
        Assert.All(state.CurrentMatches, m => Assert.Equal(PlayMatchStatus.Ready, m.Status));
    }
}
