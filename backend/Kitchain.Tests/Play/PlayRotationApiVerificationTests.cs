using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kitchain.Application.Play;
using Kitchain.Domain.Play;
using Kitchain.Infrastructure.Play;
using Kitchain.Tests.Courts;

namespace Kitchain.Tests.Play;

public sealed class PlayRotationApiVerificationTests(PostgresCourtFixture fixture) : IClassFixture<PostgresCourtFixture>
{
    private const string Route = "/api/play/sessions";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
    private async Task<string> Create(PlayRotationMode mode, int courts = 1, int players = 8)
    {
        using var created = await fixture.Client.PostAsJsonAsync(Route,
            PlaySessionServiceTests.Input with { RotationMode = mode, NumberOfCourts = courts }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var session = (await created.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!;
        var url = $"{Route}/{session.JoinCode}";
        for (var i = 0; i < players; i++)
        {
            using var added = await fixture.Client.PostAsJsonAsync($"{url}/players", new { displayName = $"Player {i}" });
            Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        }
        using var started = await fixture.Client.PostAsync($"{url}/start", null);
        await fixture.Client.StartReadyGames(url);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        return url;
    }
    private async Task<PlaySessionDetail> Read(string url) => (await fixture.Client.GetFromJsonAsync<PlaySessionDetail>(url, Json))!;
    private async Task Finish(string url, Guid match, PlayTeam? winner)
    {
        using var response = await fixture.Client.PostAsJsonAsync($"{url}/matches/{match}/finish", new FinishPlayGame { Winner = winner }, Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
    private async Task Confirm(string url, PlayMatchDetail match)
    {
        using var response = await fixture.Client.PostAsJsonAsync($"{url}/matches/{match.Id}/next",
            new StartNextPlayGame { PlayerIds = match.NextLineup.Select(p => p.PlayerId).ToArray() }, Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var canonical = await fixture.Client.StartReadyGames(url);
        Assert.Equal(match.NextLineup.Select(p => (p.PlayerId, p.Team, p.Position)),
            canonical.CurrentMatches.Single(m => m.CourtNumber == match.CourtNumber).Players.Select(p => (p.PlayerId, p.Team, p.Position)));
    }

    [PostgresTheory]
    [InlineData(PlayRotationMode.WinnersStay)]
    [InlineData(PlayRotationMode.ChallengersStay)]
    [InlineData(PlayRotationMode.SplitTeams)]
    public async Task Persisted_recommendations_are_deterministic_across_fresh_loads_and_no_result_matches_fair(PlayRotationMode mode)
    {
        var url = await Create(mode);
        for (var round = 0; round < 3; round++)
        {
            var active = Assert.Single((await Read(url)).CurrentMatches);
            await Finish(url, active.Id, round == 2 ? null : round == 0 ? PlayTeam.A : PlayTeam.B);
            var held = await Read(url);
            var proposal = Assert.Single(held.CurrentMatches);
            for (var read = 0; read < 5; read++)
            {
                var reloaded = await Read(url);
                Assert.Equal(mode, reloaded.RotationMode);
                Assert.Equal(proposal.NextLineup, Assert.Single(reloaded.CurrentMatches).NextLineup);
                // Each context materializes a fresh graph with the same persisted IDs and counters.
                await using var db = fixture.CreateDbContext();
                var aggregate = (await new EfPlaySessionStore(db).FindAsync(held.JoinCode, default))!;
                Assert.Equal(proposal.NextLineup.Select(p => p.PlayerId), aggregate.NextLineup(active.Id).Select(p => p.Id));
                if (round == 2)
                {
                    Assert.Null(proposal.Winner);
                    var fair = PlayRotationVerificationTests.Fair(aggregate, aggregate.EligibleNextPlayers(active.Id), active.Id);
                    Assert.Equal(PlayRotationVerificationTests.Pair(aggregate, fair, active.Id).Select(p => p.Id),
                        proposal.NextLineup.Select(p => p.PlayerId));
                }
            }
            if (mode == PlayRotationMode.SplitTeams && round < 2)
            {
                Assert.Single(proposal.Players, p => p.Team == PlayTeam.A && proposal.NextLineup.Any(n => n.PlayerId == p.PlayerId));
                Assert.Single(proposal.Players, p => p.Team == PlayTeam.B && proposal.NextLineup.Any(n => n.PlayerId == p.PlayerId));
            }
            await Confirm(url, proposal);
            var after = await Read(url);
            Assert.Equal(round + 1, after.MatchHistory.Count);
            Assert.Equal(proposal.Players, after.MatchHistory.Single(m => m.Id == active.Id).Players);
        }
    }

    [PostgresTheory]
    [InlineData(PlayRotationMode.WinnersStay)]
    [InlineData(PlayRotationMode.ChallengersStay)]
    [InlineData(PlayRotationMode.SplitTeams)]
    public async Task Two_courts_reject_stale_confirmation_and_preserve_canonical_holds(PlayRotationMode mode)
    {
        var url = await Create(mode, 2, 10);
        var current = (await Read(url)).CurrentMatches;
        await Finish(url, current[0].Id, PlayTeam.A);
        var firstHold = await Read(url);
        Assert.DoesNotContain(firstHold.CurrentMatches[0].NextLineup,
            p => firstHold.CurrentMatches[1].Players.Any(other => other.PlayerId == p.PlayerId));
        await Finish(url, current[1].Id, PlayTeam.B);
        var bothHeld = await Read(url);
        Assert.Equal(1, bothHeld.Queue.CourtNumber);
        await Confirm(url, bothHeld.CurrentMatches[0]);
        var afterFirst = await Read(url);
        var second = afterFirst.CurrentMatches.Single(m => m.CourtNumber == 2);
        Assert.Equal(PlayMatchStatus.Completed, second.Status);
        Assert.Equal(current[1].Players, second.Players);
        Assert.Equal(8, afterFirst.CurrentMatches.SelectMany(m => m.Players).Select(p => p.PlayerId).Distinct().Count());
        using var stale = await fixture.Client.PostAsJsonAsync($"{url}/matches/{second.Id}/next", new StartNextPlayGame
        {
            PlayerIds = bothHeld.CurrentMatches[1].NextLineup.Select(p => p.PlayerId).ToArray()
        }, Json);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var unchanged = await Read(url);
        Assert.Equal(afterFirst.UpdatedAt, unchanged.UpdatedAt);
        await Confirm(url, Assert.Single(unchanged.CurrentMatches, m => m.CourtNumber == 2));
        Assert.Equal(8, (await Read(url)).CurrentMatches.SelectMany(m => m.Players).Select(p => p.PlayerId).Distinct().Count());
    }

    [PostgresFact]
    public async Task Truly_omitted_rotation_defaults_on_create_and_preserves_draft_selection_and_rejects_bad_values()
    {
        var legacy = new { name = "Legacy", date = "2026-09-16", startTime = "18:00:00", endTime = "21:00:00", numberOfCourts = 1 };
        using var created = await fixture.Client.PostAsJsonAsync(Route, legacy);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var initial = (await created.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!;
        Assert.Equal(PlayRotationMode.FairRotation, initial.RotationMode);
        var url = $"{Route}/{initial.JoinCode}";
        foreach (var mode in Enum.GetValues<PlayRotationMode>())
        {
            using var edit = await fixture.Client.PatchAsJsonAsync(url, PlaySessionServiceTests.Input with { RotationMode = mode }, Json);
            Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
            using var omitted = await fixture.Client.PatchAsJsonAsync(url, legacy);
            Assert.Equal(HttpStatusCode.OK, omitted.StatusCode);
            Assert.Equal(mode, (await Read(url)).RotationMode);
        }
        foreach (var invalid in new object[] { "Unknown", 99, 0 })
        {
            var body = JsonSerializer.SerializeToNode(legacy)!;
            body["rotationMode"] = JsonSerializer.SerializeToNode(invalid);
            using var create = await fixture.Client.PostAsJsonAsync(Route, body);
            using var edit = await fixture.Client.PatchAsJsonAsync(url, body);
            Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, edit.StatusCode);
            Assert.Equal(PlayRotationMode.SplitTeams, (await Read(url)).RotationMode);
        }
    }
}
