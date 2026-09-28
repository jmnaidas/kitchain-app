using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kitchain.Application.Play;
using Kitchain.Domain.Play;
using Kitchain.Tests.Courts;

namespace Kitchain.Tests.Play;

public sealed class PlayNextRoundApiTests(PostgresCourtFixture fixture) : IClassFixture<PostgresCourtFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private async Task<(string Url, PlaySessionDetail State)> Create()
    {
        var response = await fixture.Client.PostAsJsonAsync("/api/play/sessions", PlaySessionServiceTests.Input with { NumberOfCourts = 2 }, Json);
        response.EnsureSuccessStatusCode();
        var draft = (await response.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!;
        var url = $"/api/play/sessions/{draft.JoinCode}";
        for (var i = 0; i < 16; i++) (await fixture.Client.PostAsJsonAsync($"{url}/players", new { displayName = $"Planner {i}" })).EnsureSuccessStatusCode();
        (await fixture.Client.PostAsync(url + "/start", null)).EnsureSuccessStatusCode();
        return (url, await fixture.Client.StartReadyGames(url));
    }
    private async Task<PlaySessionDetail> Read(string url) => (await fixture.Client.GetFromJsonAsync<PlaySessionDetail>(url, Json))!;

    [PostgresFact]
    public async Task Edits_finalize_refresh_and_proceed_preserve_the_saved_future_slots()
    {
        var (url, state) = await Create();
        var plan = state.NextRound!; var a = plan.Courts[0]; var b = plan.Courts[1];
        var swap = await fixture.Client.PatchAsJsonAsync(url + "/next-round", new { matchId = a.MatchId, position = 1,
            playerId = b.Players[2].PlayerId, expectedRevision = plan.Revision });
        swap.EnsureSuccessStatusCode();
        var edited = await Read(url);
        Assert.Equal(b.Players[2].PlayerId, edited.NextRound!.Courts[0].Players[0].PlayerId);
        Assert.Equal(a.Players[0].PlayerId, edited.NextRound.Courts[1].Players[2].PlayerId);
        Assert.Equal(JsonSerializer.Serialize(state.CurrentMatches, Json), JsonSerializer.Serialize(edited.CurrentMatches, Json));
        Assert.Equal(8, edited.Queue.NextUp.Count);
        (await fixture.Client.PostAsJsonAsync(url + "/next-round/finalize", new { expectedRevision = edited.NextRound.Revision })).EnsureSuccessStatusCode();
        var finalized = await Read(url);
        Assert.True(finalized.NextRound!.Finalized);
        Assert.Equal(JsonSerializer.Serialize(edited.NextRound.Courts, Json), JsonSerializer.Serialize(finalized.NextRound.Courts, Json));
        (await fixture.Client.PostAsync($"{url}/matches/{a.MatchId}/finish", null)).EnsureSuccessStatusCode();
        var ids = finalized.NextRound.Courts[0].Players.Select(p => p.PlayerId).ToArray();
        (await fixture.Client.PostAsJsonAsync($"{url}/matches/{a.MatchId}/next", new { playerIds = ids, overrideLineup = false })).EnsureSuccessStatusCode();
        var ready = await Read(url);
        var match = ready.CurrentMatches.Single(m => m.CourtNumber == a.CourtNumber);
        Assert.Equal(PlayMatchStatus.Ready, match.Status);
        Assert.Equal(ids, match.Players.Select(p => p.PlayerId));
        Assert.Equal(PlayMatchStatus.Active, ready.CurrentMatches.Single(m => m.CourtNumber == b.CourtNumber).Status);
        Assert.True(ready.NextRound!.Finalized);
        Assert.Single(ready.NextRound.Courts);
    }

    [PostgresFact]
    public async Task Concurrent_edit_and_finalize_accept_one_intent_and_invalid_edits_roll_back()
    {
        var (url, state) = await Create(); var plan = state.NextRound!;
        var results = await Task.WhenAll(
            fixture.Client.PatchAsJsonAsync(url + "/next-round", new { matchId = plan.Courts[0].MatchId, position = 1,
                playerId = plan.Courts[1].Players[1].PlayerId, expectedRevision = plan.Revision }),
            fixture.Client.PostAsJsonAsync(url + "/next-round/finalize", new { expectedRevision = plan.Revision }));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        var saved = await Read(url);
        Assert.Equal(8, saved.NextRound!.Courts.SelectMany(c => c.Players).Select(p => p.PlayerId).Distinct().Count());
        var invalid = await fixture.Client.PatchAsJsonAsync(url + "/next-round", new { matchId = plan.Courts[0].MatchId, position = 1,
            playerId = Guid.NewGuid(), expectedRevision = saved.NextRound.Revision });
        Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);
        Assert.Equal(JsonSerializer.Serialize(saved, Json), JsonSerializer.Serialize(await Read(url), Json));
        (await fixture.Client.PostAsync(url + "/end", null)).EnsureSuccessStatusCode();
        var ended = await Read(url);
        Assert.Empty(ended.NextRound!.Courts);
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.Client.PostAsJsonAsync(url + "/next-round/finalize",
            new { expectedRevision = ended.NextRound.Revision })).StatusCode);
    }


    [PostgresFact]
    public async Task Connected_completed_courts_promote_together_without_duplicate_current_assignments()
    {
        var (url, state) = await Create();
        // Put each current lineup in the future plan, then exchange a player across courts.
        foreach (var match in state.CurrentMatches)
            foreach (var slot in match.Players)
            {
                var revision = (await Read(url)).NextRound!.Revision;
                (await fixture.Client.PatchAsJsonAsync(url + "/next-round", new { matchId = match.Id,
                    position = slot.Position, playerId = slot.PlayerId, expectedRevision = revision })).EnsureSuccessStatusCode();
            }
        var a = state.CurrentMatches[0]; var b = state.CurrentMatches[1];
        var current = await Read(url);
        (await fixture.Client.PatchAsJsonAsync(url + "/next-round", new { matchId = a.Id, position = 1,
            playerId = b.Players.Single(p => p.Position == 1).PlayerId, expectedRevision = current.NextRound!.Revision })).EnsureSuccessStatusCode();
        current = await Read(url);
        (await fixture.Client.PostAsJsonAsync(url + "/next-round/finalize",
            new { expectedRevision = current.NextRound!.Revision })).EnsureSuccessStatusCode();
        var approved = (await Read(url)).NextRound!;
        (await fixture.Client.PostAsync($"{url}/matches/{a.Id}/finish", null)).EnsureSuccessStatusCode();
        var held = await Read(url);
        var ids = approved.Courts[0].Players.Select(p => p.PlayerId).ToArray();
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.Client.PostAsJsonAsync($"{url}/matches/{a.Id}/next",
            new { playerIds = ids, overrideLineup = false })).StatusCode);
        Assert.Equal(JsonSerializer.Serialize(held, Json), JsonSerializer.Serialize(await Read(url), Json));
        (await fixture.Client.PostAsync($"{url}/matches/{b.Id}/finish", null)).EnsureSuccessStatusCode();
        (await fixture.Client.PostAsJsonAsync($"{url}/matches/{a.Id}/next", new { playerIds = ids, overrideLineup = false })).EnsureSuccessStatusCode();
        var ready = await Read(url);
        Assert.Equal(2, ready.CurrentMatches.Count);
        Assert.All(ready.CurrentMatches, m => Assert.Equal(PlayMatchStatus.Ready, m.Status));
        Assert.Equal(8, ready.CurrentMatches.SelectMany(m => m.Players).Select(p => p.PlayerId).Distinct().Count());
        foreach (var court in approved.Courts)
            Assert.Equal(court.Players.Select(p => p.PlayerId), ready.CurrentMatches.Single(m => m.CourtNumber == court.CourtNumber).Players.Select(p => p.PlayerId));
    }
}
