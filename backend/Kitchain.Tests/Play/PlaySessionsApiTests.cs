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

// Reuses the existing isolated-schema PostgreSQL test infrastructure; never the application's tables.
public sealed class PlaySessionsApiTests(PostgresCourtFixture fixture) : IClassFixture<PostgresCourtFixture>
{
    private const string Route = "/api/play/sessions";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    [PostgresFact]
    public async Task Api_creates_guest_queue_and_persists_rest_rejoin_and_start_with_expected_status_codes()
    {
        using var create = await fixture.Client.PostAsJsonAsync(Route, PlaySessionServiceTests.Input, Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(create.Headers.Location);
        var session = await create.Content.ReadFromJsonAsync<PlaySessionDetail>(Json);
        Assert.NotNull(session);
        Assert.Equal(PlaySessionStatus.Draft, session.Status);
        var url = $"{Route}/{session.JoinCode.ToLowerInvariant()}";
        using var first = await fixture.Client.PostAsJsonAsync($"{url}/players", new { displayName = " Alex " });
        using var second = await fixture.Client.PostAsJsonAsync($"{url}/players", new { displayName = "Sam" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var alex = await first.Content.ReadFromJsonAsync<PlayPlayerDetail>(Json);
        var sam = await second.Content.ReadFromJsonAsync<PlayPlayerDetail>(Json);
        Assert.NotNull(alex);
        Assert.NotNull(sam);
        using var duplicate = await fixture.Client.PostAsJsonAsync($"{url}/players", new { displayName = "aLeX" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var rest = await fixture.Client.PostAsync($"{url}/players/{alex.Id}/rest", null);
        Assert.Equal(HttpStatusCode.OK, rest.StatusCode);
        var resting = await fixture.Client.GetFromJsonAsync<PlaySessionDetail>(url, Json);
        Assert.NotNull(resting);
        Assert.Equal(sam.Id, Assert.Single(resting.WaitingQueue).Id);
        using var rejoin = await fixture.Client.PostAsync($"{url}/players/{alex.Id}/rejoin", null);
        Assert.Equal(HttpStatusCode.OK, rejoin.StatusCode);
        var reordered = await fixture.Client.GetFromJsonAsync<PlaySessionDetail>(url, Json);
        Assert.NotNull(reordered);
        Assert.Equal(new[] { sam.Id, alex.Id }, reordered.WaitingQueue.Select(p => p.Id));
        Assert.Equal(new long?[] { 2, 3 }, reordered.WaitingQueue.Select(p => p.QueueOrder));
        using var start = await fixture.Client.PostAsync($"{url}/start", null);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        Assert.Equal(PlaySessionStatus.Active, (await start.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!.Status);
        using var repeatedStart = await fixture.Client.PostAsync($"{url}/start", null);
        using var missingPlayer = await fixture.Client.PostAsync($"{url}/players/{Guid.NewGuid()}/rest", null);
        using var missingSession = await fixture.Client.GetAsync($"{Route}/not-a-code");
        using var invalid = await fixture.Client.PostAsJsonAsync($"{url}/players", new { displayName = " " });
        using var clientStatus = await fixture.Client.PostAsJsonAsync(Route, new { name = "Play", status = "Active" });
        Assert.Equal(HttpStatusCode.Conflict, repeatedStart.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingPlayer.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingSession.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, clientStatus.StatusCode);
        await using var db = fixture.CreateDbContext();
        var ended = await db.Set<PlaySession>().SingleAsync(s => s.Id == session.Id);
        ended.End(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        using var late = await fixture.Client.PostAsJsonAsync($"{url}/players", new { displayName = "Lee" });
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
    }

    [PostgresFact]
    public async Task Concurrent_joins_enforce_capacity_and_allocate_distinct_persisted_fifo_tickets()
    {
        using var response = await fixture.Client.PostAsJsonAsync(Route, PlaySessionServiceTests.Input with { MaximumPlayers = 2 }, Json);
        var session = await response.Content.ReadFromJsonAsync<PlaySessionDetail>(Json);
        Assert.NotNull(session);
        var url = $"{Route}/{session.JoinCode}";
        var joins = await Task.WhenAll(new[] { "Alex", "Sam", "Lee" }.Select(name =>
            fixture.Client.PostAsJsonAsync($"{url}/players", new { displayName = name })));
        try
        {
            Assert.Equal(2, joins.Count(r => r.StatusCode == HttpStatusCode.Created));
            Assert.Single(joins, r => r.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var join in joins) join.Dispose(); }
        var saved = await fixture.Client.GetFromJsonAsync<PlaySessionDetail>(url, Json);
        Assert.NotNull(saved);
        Assert.Equal(2, saved.Players.Count);
        Assert.Equal(new long?[] { 1, 2 }, saved.WaitingQueue.Select(p => p.QueueOrder));
    }

    [PostgresFact]
    public async Task Database_join_code_uniqueness_triggers_retry_instead_of_overwriting_a_session()
    {
        await using var db = fixture.CreateDbContext();
        var store = new EfPlaySessionStore(db);
        var first = await new PlaySessionService(store, new PlaySessionServiceTests.SequenceCodes("ABCDEF"))
            .CreateAsync(PlaySessionServiceTests.Input, default);
        var second = await new PlaySessionService(store, new PlaySessionServiceTests.SequenceCodes("ABCDEF", "GHJKLM"))
            .CreateAsync(PlaySessionServiceTests.Input, default);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal("GHJKLM", second.JoinCode);
        Assert.Equal(1, await db.Set<PlaySession>().CountAsync(s => s.JoinCode == "ABCDEF"));
        Assert.Equal(1, await db.Set<PlaySession>().CountAsync(s => s.JoinCode == "GHJKLM"));
    }
}
