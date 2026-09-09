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

    [PostgresFact]
    public async Task Concurrent_finish_persists_one_completion_and_one_replacement_without_duplicate_players()
    {
        using var created = await fixture.Client.PostAsJsonAsync(Route, PlaySessionServiceTests.Input with { NumberOfCourts = 1 }, Json);
        var session = (await created.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!;
        var url = $"{Route}/{session.JoinCode}";
        var ids = new List<Guid>();
        for (var i = 1; i <= 8; i++)
        {
            using var added = await fixture.Client.PostAsJsonAsync($"{url}/players", new { displayName = $"Player {i}" });
            Assert.Equal(HttpStatusCode.Created, added.StatusCode);
            ids.Add((await added.Content.ReadFromJsonAsync<PlayPlayerDetail>(Json))!.Id);
        }
        using var started = await fixture.Client.PostAsync($"{url}/start", null);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        var active = (await started.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!;
        var match = Assert.Single(active.ActiveMatches);
        var finishes = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            fixture.Client.PostAsync($"{url}/matches/{match.Id}/finish", null)));
        try
        {
            Assert.Single(finishes, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(finishes, response => response.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var response in finishes) response.Dispose(); }
        var saved = (await fixture.Client.GetFromJsonAsync<PlaySessionDetail>(url, Json))!;
        var replacement = Assert.Single(saved.ActiveMatches);
        Assert.NotEqual(match.Id, replacement.Id);
        Assert.Equal(ids.Skip(4), replacement.Players.Select(p => p.PlayerId));
        Assert.Equal(ids.Take(4), saved.WaitingQueue.Select(p => p.Id));
        Assert.Equal(4, saved.Players.Count(p => p.State == PlayPlayerState.Playing));
        await using var db = fixture.CreateDbContext();
        var history = await db.Set<PlayMatch>().AsNoTracking().Include(m => m.Players)
            .Where(m => m.SessionId == session.Id).ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.NotNull(history.Single(m => m.Status == PlayMatchStatus.Completed).CompletedAt);
        Assert.All(history, game => Assert.Equal(4, game.Players.Count));
    }
    [PostgresFact]
    public async Task Concurrent_rallies_preserve_points_and_complete_rotate_only_once()
    {
        using var created = await fixture.Client.PostAsJsonAsync(Route, PlaySessionServiceTests.Input with { NumberOfCourts = 1 }, Json);
        var session = (await created.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!;
        var url = $"{Route}/{session.JoinCode}";
        for (var i = 1; i <= 4; i++)
        {
            using var added = await fixture.Client.PostAsJsonAsync($"{url}/players", new { displayName = $"Scorer {i}" });
            Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        }
        using var started = await fixture.Client.PostAsync($"{url}/start", null);
        var match = Assert.Single((await started.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!.ActiveMatches);
        var rallyUrl = $"{url}/matches/{match.Id}/rallies";
        var rallies = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => fixture.Client.PostAsJsonAsync(rallyUrl, new { winner = "A" })));
        try { Assert.All(rallies, result => Assert.Equal(HttpStatusCode.OK, result.StatusCode)); }
        finally { foreach (var result in rallies) result.Dispose(); }
        var saved = (await fixture.Client.GetFromJsonAsync<PlaySessionDetail>(url, Json))!;
        Assert.Equal(2, Assert.Single(saved.ActiveMatches).TeamAScore);
        using var corrected = await fixture.Client.PatchAsJsonAsync($"{url}/matches/{match.Id}/score", new { teamAScore = 9, teamBScore = 9, servingTeam = "A", currentServerNumber = 2 });
        Assert.Equal(HttpStatusCode.OK, corrected.StatusCode);
        var winning = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => fixture.Client.PostAsJsonAsync(rallyUrl, new { winner = "A" })));
        try
        {
            Assert.Equal(2, winning.Count(result => result.StatusCode == HttpStatusCode.OK));
            Assert.Single(winning, result => result.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var result in winning) result.Dispose(); }
        await using var db = fixture.CreateDbContext();
        var games = await db.Set<PlayMatch>().Where(m => m.SessionId == session.Id).ToListAsync();
        Assert.Equal(2, games.Count);
        Assert.Equal(11, games.Single(m => m.Status == PlayMatchStatus.Completed).TeamAScore);
        Assert.Equal(0, games.Single(m => m.Status == PlayMatchStatus.Active).TeamAScore);
        using var invalid = await fixture.Client.PostAsJsonAsync(rallyUrl, new { winner = "C" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }
}
