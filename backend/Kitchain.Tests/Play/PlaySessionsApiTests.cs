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

    [PostgresTheory]
    [InlineData(PlayRotationMode.FairRotation)]
    [InlineData(PlayRotationMode.WinnersStay)]
    [InlineData(PlayRotationMode.ChallengersStay)]
    [InlineData(PlayRotationMode.SplitTeams)]
    public async Task Rotation_configuration_persists_through_create_edit_reload_and_lifecycle(PlayRotationMode mode)
    {
        using var created = await fixture.Client.PostAsJsonAsync(Route, PlaySessionServiceTests.Input with { RotationMode = mode }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var session = (await created.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!;
        var url = $"{Route}/{session.JoinCode}";
        Assert.Equal(mode, (await fixture.Client.GetFromJsonAsync<PlaySessionDetail>(url, Json))!.RotationMode);
        using var legacyEdit = await fixture.Client.PatchAsJsonAsync(url, PlaySessionServiceTests.Input, Json);
        Assert.Equal(HttpStatusCode.OK, legacyEdit.StatusCode);
        Assert.Equal(mode, (await legacyEdit.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!.RotationMode);
        using var edit = await fixture.Client.PatchAsJsonAsync(url, PlaySessionServiceTests.Input with { RotationMode = PlayRotationMode.SplitTeams }, Json);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        await using var db = fixture.CreateDbContext();
        Assert.Equal(PlayRotationMode.SplitTeams, (await db.Set<PlaySession>().AsNoTracking().SingleAsync(s => s.Id == session.Id)).RotationMode);
        using var invalid = await fixture.Client.PatchAsJsonAsync(url, new
        {
            name = "Crew", date = "2026-09-16", startTime = "18:00:00", endTime = "21:00:00",
            numberOfCourts = 1, rotationMode = "Unsupported"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var started = await fixture.Client.PostAsync($"{url}/start", null);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        using var activeEdit = await fixture.Client.PatchAsJsonAsync(url, PlaySessionServiceTests.Input with { RotationMode = mode }, Json);
        Assert.Equal(HttpStatusCode.Conflict, activeEdit.StatusCode);
        using var ended = await fixture.Client.PostAsync($"{url}/end", null);
        Assert.Equal(HttpStatusCode.OK, ended.StatusCode);
        using var endedEdit = await fixture.Client.PatchAsJsonAsync(url, PlaySessionServiceTests.Input with { RotationMode = mode }, Json);
        Assert.Equal(HttpStatusCode.Conflict, endedEdit.StatusCode);
        Assert.Equal(PlayRotationMode.SplitTeams, (await fixture.Client.GetFromJsonAsync<PlaySessionDetail>(url, Json))!.RotationMode);
    }

    [PostgresFact]
    public async Task Api_creates_guest_queue_and_persists_rest_rejoin_and_start_with_expected_status_codes()
    {
        using var create = await fixture.Client.PostAsJsonAsync(Route, PlaySessionServiceTests.Input, Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(create.Headers.Location);
        var session = await create.Content.ReadFromJsonAsync<PlaySessionDetail>(Json);
        Assert.NotNull(session);
        Assert.Equal(PlaySessionStatus.Draft, session.Status);
        Assert.Equal(PlayRotationMode.FairRotation, session.RotationMode);
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
        using var draftRest = await fixture.Client.PostAsync($"{url}/players/{alex.Id}/rest", null);
        Assert.Equal(HttpStatusCode.Conflict, draftRest.StatusCode);
        using var start = await fixture.Client.PostAsync($"{url}/start", null);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        Assert.Equal(PlaySessionStatus.Active, (await start.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!.Status);
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
    public async Task Concurrent_completion_and_confirmation_each_succeed_once_without_duplicate_players()
    {
        using var created = await fixture.Client.PostAsJsonAsync(Route, PlaySessionServiceTests.Input with { NumberOfCourts = 1, Mode = PlaySessionMode.LiveScoring }, Json);
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
        Assert.Empty(saved.ActiveMatches);
        var held = Assert.Single(saved.CurrentMatches);
        Assert.Equal(match.Id, held.Id);
        Assert.Equal(PlayMatchStatus.Completed, held.Status);
        Assert.Null(held.Winner);
        Assert.Equal(ids.Skip(4).Order(), held.NextLineup.Select(p => p.PlayerId).Order());
        Assert.Equal(ids.Skip(4), saved.WaitingQueue.Select(p => p.Id));
        var confirmations = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            fixture.Client.PostAsJsonAsync($"{url}/matches/{match.Id}/next", new { playerIds = held.NextLineup.Select(p => p.PlayerId), overrideLineup = false })));
        try
        {
            Assert.Single(confirmations, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(confirmations, response => response.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var response in confirmations) response.Dispose(); }
        saved = (await fixture.Client.GetFromJsonAsync<PlaySessionDetail>(url, Json))!;
        var replacement = Assert.Single(saved.ActiveMatches);
        Assert.NotEqual(match.Id, replacement.Id);
        Assert.Equal(held.NextLineup.Select(p => p.PlayerId), replacement.Players.Select(p => p.PlayerId));
        Assert.Equal(match.Players.Select(p => p.PlayerId), saved.WaitingQueue.Select(p => p.Id));
        Assert.Equal(4, saved.Players.Count(p => p.State == PlayPlayerState.Playing));
        await using var db = fixture.CreateDbContext();
        var history = await db.Set<PlayMatch>().AsNoTracking().Include(m => m.Players)
            .Where(m => m.SessionId == session.Id).ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.NotNull(history.Single(m => m.Status == PlayMatchStatus.Completed).CompletedAt);
        Assert.All(history, game => Assert.Equal(4, game.Players.Count));
    }
    [PostgresFact]
    public async Task Concurrent_rallies_preserve_points_and_complete_once_without_rotation()
    {
        using var created = await fixture.Client.PostAsJsonAsync(Route, PlaySessionServiceTests.Input with { NumberOfCourts = 1, Mode = PlaySessionMode.LiveScoring }, Json);
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
        Assert.Equal(2, Assert.Single(saved.ActiveMatches).Rallies.Count);
        Assert.Equal(PlaySessionMode.LiveScoring, saved.Mode);
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
        Assert.Single(games);
        Assert.Equal(11, games.Single(m => m.Status == PlayMatchStatus.Completed).TeamAScore);
        Assert.True(games[0].IsCurrent);
        Assert.Equal(PlayTeam.A, games[0].Winner);
        saved = (await fixture.Client.GetFromJsonAsync<PlaySessionDetail>(url, Json))!;
        Assert.Empty(saved.ActiveMatches);
        Assert.Equal(new long[] { 1, 2, 3, 4 }, Assert.Single(saved.CurrentMatches).Rallies.Select(r => r.Sequence));
        Assert.Equal(saved.Players.Select(p => p.Id).Order(), Assert.Single(saved.CurrentMatches).NextLineup.Select(p => p.PlayerId).Order());
        Assert.All(saved.Players, player => Assert.Equal(PlayPlayerState.Playing, player.State));
        using var invalid = await fixture.Client.PostAsJsonAsync(rallyUrl, new { winner = "C" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }
    [PostgresFact]
    public async Task Draft_changes_default_mode_and_ended_current_state_survive_reload()
    {
        using var created = await fixture.Client.PostAsJsonAsync(Route, PlaySessionServiceTests.Input with { NumberOfCourts = 1 }, Json);
        var session = (await created.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!;
        Assert.Equal(PlaySessionMode.QueueOnly, session.Mode);
        var url = $"{Route}/{session.JoinCode}";
        for (var i = 0; i < 9; i++)
        {
            using var added = await fixture.Client.PostAsJsonAsync($"{url}/players", new { displayName = $"Guest {i}" });
            Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        }
        var draft = (await fixture.Client.GetFromJsonAsync<PlaySessionDetail>(url, Json))!;
        var first = draft.WaitingQueue[0];
        var removed = draft.WaitingQueue[1];
        using var renamed = await fixture.Client.PatchAsJsonAsync($"{url}/players/{first.Id}", new { displayName = " New Name " });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        using var duplicate = await fixture.Client.PatchAsJsonAsync($"{url}/players/{removed.Id}", new { displayName = "NEW  NAME" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var deleted = await fixture.Client.DeleteAsync($"{url}/players/{removed.Id}");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        draft = (await fixture.Client.GetFromJsonAsync<PlaySessionDetail>(url, Json))!;
        Assert.Equal("New Name", draft.WaitingQueue[0].DisplayName);
        Assert.Equal(new long?[] { 1, 3, 4, 5, 6, 7, 8, 9 }, draft.WaitingQueue.Select(p => p.QueueOrder));
        using var started = await fixture.Client.PostAsync($"{url}/start", null);
        var active = (await started.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!;
        var match = Assert.Single(active.CurrentMatches);
        using var scoring = await fixture.Client.PostAsJsonAsync($"{url}/matches/{match.Id}/rallies", new { winner = "A" });
        Assert.Equal(HttpStatusCode.Conflict, scoring.StatusCode);
        using var activeRename = await fixture.Client.PatchAsJsonAsync($"{url}/players/{first.Id}", new { displayName = "Active name" });
        using var forbiddenRemove = await fixture.Client.DeleteAsync($"{url}/players/{match.Players[0].PlayerId}");
        Assert.Equal(HttpStatusCode.OK, activeRename.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, forbiddenRemove.StatusCode);
        using var finish = await fixture.Client.PostAsync($"{url}/matches/{match.Id}/finish", null);
        Assert.Equal(HttpStatusCode.OK, finish.StatusCode);
        using var ended = await fixture.Client.PostAsync($"{url}/end", null);
        Assert.Equal(HttpStatusCode.OK, ended.StatusCode);
        var saved = (await fixture.Client.GetFromJsonAsync<PlaySessionDetail>(url, Json))!;
        Assert.Equal(PlaySessionStatus.Ended, saved.Status);
        Assert.Equal(PlayMatchStatus.Completed, Assert.Single(saved.CurrentMatches).Status);
        Assert.Null(saved.CurrentMatches[0].Winner);
        Assert.Empty(saved.CurrentMatches[0].NextLineup);
        Assert.Equal(active.WaitingQueue.Select(p => p.Id), saved.WaitingQueue.Select(p => p.Id));
        using var next = await fixture.Client.PostAsJsonAsync($"{url}/matches/{match.Id}/next", new { playerIds = saved.WaitingQueue.Select(p => p.Id), overrideLineup = false });
        Assert.Equal(HttpStatusCode.Conflict, next.StatusCode);
        await using var db = fixture.CreateDbContext();
        Assert.False(await db.Set<PlaySessionPlayer>().AnyAsync(p => p.Id == removed.Id));
        Assert.Single(await db.Set<PlayMatch>().Where(m => m.SessionId == session.Id).ToListAsync());
    }

    [PostgresFact]
    public async Task Overnight_draft_edit_and_fairness_state_survive_reload_and_invalid_edits_are_atomic()
    {
        using var created = await fixture.Client.PostAsJsonAsync(Route, PlaySessionServiceTests.Input, Json);
        var session = (await created.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!;
        Assert.Equal(session.SessionDate, session.EndDate);
        var url = $"{Route}/{session.JoinCode}";
        for (var i = 0; i < 10; i++)
        {
            using var added = await fixture.Client.PostAsJsonAsync($"{url}/players", new { displayName = $"Fair {i}" });
            Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        }
        var input = PlaySessionServiceTests.Input with { Name = "Overnight", StartTime = new(23, 0), EndDate = new(2026, 9, 11), EndTime = new(2, 0), MaximumPlayers = 10, Mode = PlaySessionMode.LiveScoring };
        using var edited = await fixture.Client.PatchAsJsonAsync(url, input, Json);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var draft = (await fixture.Client.GetFromJsonAsync<PlaySessionDetail>(url, Json))!;
        Assert.Equal(input.EndDate, draft.EndDate);
        Assert.Equal(input.Mode, draft.Mode);
        using var invalid = await fixture.Client.PatchAsJsonAsync(url, input with { Name = "Must not save", MaximumPlayers = 9 }, Json);
        Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);
        using var invalidDate = await fixture.Client.PatchAsJsonAsync(url, input with { EndDate = input.Date }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, invalidDate.StatusCode);
        using var started = await fixture.Client.PostAsync($"{url}/start", null);
        var active = (await started.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!;
        using var forbidden = await fixture.Client.PatchAsJsonAsync(url, input, Json);
        Assert.Equal(HttpStatusCode.Conflict, forbidden.StatusCode);
        var match = active.CurrentMatches[0];
        using var finished = await fixture.Client.PostAsync($"{url}/matches/{match.Id}/finish", null);
        var held = (await finished.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!;
        var proposal = held.CurrentMatches.Single(m => m.Id == match.Id).NextLineup;
        Assert.Equal(2, proposal.Count(p => active.WaitingQueue.Any(w => w.Id == p.PlayerId)));
        await using (var before = fixture.CreateDbContext())
        {
            var players = await before.Set<PlaySessionPlayer>().Where(p => p.SessionId == session.Id).ToListAsync();
            Assert.Equal(8, players.Sum(p => p.AdjustedGamesStarted));
            Assert.All(players.Where(p => p.State == PlayPlayerState.Waiting), p => { Assert.Equal(2, p.MissedOpportunities); Assert.NotNull(p.WaitingSince); });
        }
        using var confirmed = await fixture.Client.PostAsJsonAsync($"{url}/matches/{match.Id}/next", new { playerIds = proposal.Select(p => p.PlayerId), overrideLineup = false });
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        await using var saved = fixture.CreateDbContext();
        var savedSession = await saved.Set<PlaySession>().SingleAsync(s => s.Id == session.Id);
        Assert.Equal("Overnight", savedSession.Name);
        Assert.Equal(input.EndDate, savedSession.EndDate);
        var savedPlayers = await saved.Set<PlaySessionPlayer>().Where(p => p.SessionId == session.Id).ToListAsync();
        Assert.Equal(12, savedPlayers.Sum(p => p.AdjustedGamesStarted));
        Assert.All(savedPlayers.Where(p => proposal.Any(slot => slot.PlayerId == p.Id)), p => { Assert.Equal(0, p.MissedOpportunities); Assert.Null(p.WaitingSince); });
        Assert.Equal(3, await saved.Set<PlayMatch>().CountAsync(m => m.SessionId == session.Id));
    }

    [PostgresFact]
    public async Task Historical_pairs_survive_reload_and_manual_continuation_is_persisted_without_double_booking()
    {
        using var created = await fixture.Client.PostAsJsonAsync(Route, PlaySessionServiceTests.Input with { NumberOfCourts = 1 }, Json);
        var session = (await created.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!;
        var url = $"{Route}/{session.JoinCode}";
        for (var i = 0; i < 8; i++)
        {
            using var added = await fixture.Client.PostAsJsonAsync($"{url}/players", new { displayName = $"Pairing {i}" });
            Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        }
        using var started = await fixture.Client.PostAsync($"{url}/start", null);
        var state = (await started.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!;
        var original = state.CurrentMatches[0].Players.Select(p => p.PlayerId).ToArray();
        var waiting = state.WaitingQueue.Select(p => p.Id).ToArray();
        // The same teams actually play twice; previews do not create history.
        foreach (var selected in new[] { original, waiting, waiting, waiting })
        {
            var current = state.CurrentMatches[0];
            using var finish = await fixture.Client.PostAsync($"{url}/matches/{current.Id}/finish", null);
            Assert.Equal(HttpStatusCode.OK, finish.StatusCode);
            state = (await fixture.Client.GetFromJsonAsync<PlaySessionDetail>(url, Json))!;
            Assert.All(selected, id => Assert.Contains(state.CurrentMatches[0].EligiblePlayers, p => p.Id == id));
            using var next = await fixture.Client.PostAsJsonAsync($"{url}/matches/{current.Id}/next", new { playerIds = selected, overrideLineup = true });
            Assert.Equal(HttpStatusCode.OK, next.StatusCode);
            state = (await next.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!;
            Assert.Equal(selected, state.CurrentMatches[0].Players.Select(p => p.PlayerId));
        }
        var heldId = state.CurrentMatches[0].Id;
        using var completed = await fixture.Client.PostAsync($"{url}/matches/{heldId}/finish", null);
        state = (await fixture.Client.GetFromJsonAsync<PlaySessionDetail>(url, Json))!;
        var proposal = state.CurrentMatches[0].NextLineup;
        Assert.Equal(original.Order(), proposal.Select(p => p.PlayerId).Order());
        Assert.NotEqual(proposal.Single(p => p.PlayerId == original[0]).Team, proposal.Single(p => p.PlayerId == original[1]).Team);
        var reread = (await fixture.Client.GetFromJsonAsync<PlaySessionDetail>(url, Json))!;
        Assert.Equal(proposal.Select(p => p.PlayerId), reread.CurrentMatches[0].NextLineup.Select(p => p.PlayerId));
        await using (var db = fixture.CreateDbContext())
            Assert.Equal(5, await db.Set<PlayMatch>().CountAsync(m => m.SessionId == session.Id));
        using var confirmed = await fixture.Client.PostAsJsonAsync($"{url}/matches/{heldId}/next", new { playerIds = proposal.Select(p => p.PlayerId), overrideLineup = false });
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        state = (await confirmed.Content.ReadFromJsonAsync<PlaySessionDetail>(Json))!;
        Assert.Equal(proposal.Select(p => p.PlayerId), Assert.Single(state.CurrentMatches).Players.Select(p => p.PlayerId));
        Assert.Equal(4, state.Players.Count(p => p.State == PlayPlayerState.Playing));
        Assert.Equal(waiting, state.WaitingQueue.Select(p => p.Id));
        await using var saved = fixture.CreateDbContext();
        Assert.Equal(6, await saved.Set<PlayMatch>().CountAsync(m => m.SessionId == session.Id));
        Assert.Equal(1, await saved.Set<PlayMatch>().CountAsync(m => m.SessionId == session.Id && m.IsCurrent));
    }
}
