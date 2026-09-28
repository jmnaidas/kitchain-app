using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kitchain.Application.Play;
using Kitchain.Domain.Play;
using Kitchain.Tests.Courts;

namespace Kitchain.Tests.Play;

public sealed class PlaySkillOpeningApiTests(PostgresCourtFixture fixture) : IClassFixture<PostgresCourtFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private async Task<(string Url, PlaySessionDetail State)> Create()
    {
        var response = await fixture.Client.PostAsJsonAsync("/api/play/sessions", PlaySessionServiceTests.Input with
            { NumberOfCourts = 2, RotationMode = PlayRotationMode.BalancedRotation }, Json);
        response.EnsureSuccessStatusCode();
        var draft = (await response.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!;
        var url = $"/api/play/sessions/{draft.JoinCode}";
        for (var i = 0; i < 9; i++)
            (await fixture.Client.PostAsJsonAsync(url + "/players", new { displayName = "Opening " + i })).EnsureSuccessStatusCode();
        return (url, await Read(url));
    }
    private async Task<PlaySessionDetail> Read(string url) => (await fixture.Client.GetFromJsonAsync<PlaySessionDetail>(url, Json))!;

    [PostgresFact]
    public async Task Skills_and_draft_swaps_reload_and_start_preserves_the_exact_saved_opening()
    {
        var (url, state) = await Create(); var id = state.Players[0].Id;
        Assert.All(state.Players, p => Assert.Null(p.SkillLevel));
        foreach (var level in Enum.GetNames<PlaySkillLevel>())
        {
            (await fixture.Client.PatchAsJsonAsync($"{url}/players/{id}/skill",
                new { skillLevel = level, expectedRevision = state.NextRound!.Revision })).EnsureSuccessStatusCode();
            state = await Read(url);
            Assert.Equal(Enum.Parse<PlaySkillLevel>(level), state.Players.Single(p => p.Id == id).SkillLevel);
        }
        var plan = state.NextRound!;
        var swap = new { matchId = plan.Courts[0].MatchId, position = 1, playerId = plan.Courts[1].Players[2].PlayerId, expectedRevision = plan.Revision };
        (await fixture.Client.PatchAsJsonAsync(url + "/next-round", swap)).EnsureSuccessStatusCode();
        state = await Read(url);
        Assert.Equal(swap.playerId, state.NextRound!.Courts[0].Players[0].PlayerId);
        Assert.Equal(plan.Courts[0].Players[0].PlayerId, state.NextRound.Courts[1].Players[2].PlayerId);
        var saved = state.NextRound.Courts;
        (await fixture.Client.PostAsync(url + "/start", null)).EnsureSuccessStatusCode();
        state = await Read(url);
        foreach (var court in saved)
        {
            var ready = state.CurrentMatches.Single(m => m.CourtNumber == court.CourtNumber);
            Assert.Equal(PlayMatchStatus.Ready, ready.Status);
            Assert.Equal(court.Players.Select(p => p.PlayerId), ready.Players.Select(p => p.PlayerId));
        }
        (await fixture.Client.PatchAsJsonAsync($"{url}/players/{id}/skill",
            new { skillLevel = (string?)null, expectedRevision = state.NextRound!.Revision })).EnsureSuccessStatusCode();
        Assert.Null((await Read(url)).Players.Single(p => p.Id == id).SkillLevel);
    }

    [PostgresTheory]
    [InlineData("99")]
    [InlineData("1")]
    [InlineData("\"1\"")]
    [InlineData("\"Expert\"")]
    [InlineData("\"Beginner, Advanced\"")]
    public async Task Invalid_skill_input_is_rejected_without_changing_persisted_state(string level)
    {
        var (url, state) = await Create();
        using var body = new StringContent($$"""{"skillLevel":{{level}},"expectedRevision":{{state.NextRound!.Revision}}}""", Encoding.UTF8, "application/json");
        var response = await fixture.Client.PatchAsync($"{url}/players/{state.Players[0].Id}/skill", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(JsonSerializer.Serialize(state, Json), JsonSerializer.Serialize(await Read(url), Json));
    }

    [PostgresFact]
    public async Task Concurrent_draft_edits_accept_one_intent_and_ended_sessions_reject_mutations()
    {
        var (url, state) = await Create(); var plan = state.NextRound!;
        var results = await Task.WhenAll(new[] { 1, 2 }.Select(position => fixture.Client.PatchAsJsonAsync(url + "/next-round",
            new { matchId = plan.Courts[0].MatchId, position, playerId = plan.Courts[1].Players[0].PlayerId, expectedRevision = plan.Revision })));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        var current = await Read(url);
        Assert.Equal(8, current.NextRound!.Courts.SelectMany(c => c.Players).Select(p => p.PlayerId).Distinct().Count());
        (await fixture.Client.PostAsync(url + "/start", null)).EnsureSuccessStatusCode();
        (await fixture.Client.PostAsync(url + "/end", null)).EnsureSuccessStatusCode();
        current = await Read(url);
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.Client.PatchAsJsonAsync($"{url}/players/{state.Players[0].Id}/skill",
            new { skillLevel = "Advanced", expectedRevision = current.NextRound!.Revision })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.Client.PatchAsJsonAsync(url + "/next-round",
            new { matchId = plan.Courts[0].MatchId, position = 1, playerId = state.Players[0].Id, expectedRevision = current.NextRound.Revision })).StatusCode);
    }
}
