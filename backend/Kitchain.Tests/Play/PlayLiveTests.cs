using System.Security.Claims;
using Kitchain.Api.Play;
using Kitchain.Application.Play;
using Kitchain.Domain.Play;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kitchain.Tests.Play;

public sealed class PlayLiveTests
{
    [Fact]
    public async Task Hub_normalizes_group_join_and_leave_and_rejects_invalid_codes()
    {
        var groups = new Groups();
        var hub = new PlayHub { Context = new Caller(), Groups = groups };
        await hub.JoinSessionGroup(" abcdef ");
        await hub.LeaveSessionGroup("abcdef");
        Assert.Equal(new[] { "+connection:Session:ABCDEF", "-connection:Session:ABCDEF" }, groups.Calls);
        await Assert.ThrowsAsync<HubException>(() => hub.JoinSessionGroup("bad"));
    }

    [Fact]
    public async Task Successful_mutations_notify_only_after_store_success_and_failures_do_not_notify()
    {
        var store = new Store();
        var service = new PlaySessionService(store, new PlaySessionServiceTests.SequenceCodes("ABCDEF"));
        var notifier = new RecordingNotifier(store);
        var controller = new PlaySessionsController(service, NullLogger<PlaySessionsController>.Instance, notifier);
        var player = store.Session.Players.Last();
        await controller.AddGuest(" abcdef ", new AddPlayGuest("Late"), default);
        await controller.Rest("ABCDEF", player.Id, default);
        await controller.Rejoin("ABCDEF", player.Id, default);
        await controller.Start("ABCDEF", default);
        var match = store.Session.Matches.Single();
        await controller.FinishGame("ABCDEF", match.Id, default);
        Assert.Equal(5, notifier.Codes.Count);
        Assert.All(notifier.Codes, code => Assert.Equal("ABCDEF", code));
        await controller.Start("ABCDEF", default);
        await controller.FinishGame("ABCDEF", match.Id, default);
        await controller.AddGuest("ABCDEF", new AddPlayGuest("Late"), default);
        await controller.AddGuest("GHJKLM", new AddPlayGuest("Other"), default);
        Assert.Equal(5, notifier.Codes.Count);
    }

    [Fact]
    public async Task Notifier_sends_only_to_the_requested_group_and_delivery_failure_does_not_fail_a_commit()
    {
        var clients = new RecordingClients();
        var notifier = new SignalRPlaySessionNotifier(new HubContext(clients), NullLogger<SignalRPlaySessionNotifier>.Instance);
        await notifier.ChangedAsync(" abcdef ");
        Assert.Equal("Session:ABCDEF", Assert.Single(clients.Targets));
        var message = Assert.Single(clients.Proxy.Messages);
        Assert.Equal("SessionChanged", message.Method);
        Assert.Equal("ABCDEF", Assert.IsType<PlaySessionChanged>(Assert.Single(message.Args)).Code);
        clients.Proxy.Fail = true;
        await notifier.ChangedAsync("GHJKLM");
        Assert.Equal(new[] { "Session:ABCDEF", "Session:GHJKLM" }, clients.Targets);
    }

    private sealed class Store : IPlaySessionStore
    {
        public PlaySession Session { get; } = new(Guid.NewGuid(), "ABCDEF", "Crew", new(2026, 9, 10), new(18, 0), new(21, 0), 1, null, DateTimeOffset.UtcNow);
        public bool Saved { get; private set; }
        public Store() { for (var i = 1; i <= 5; i++) Session.AddGuest($"Player {i}", Session.UpdatedAt); }
        public Task<bool> TryAddAsync(PlaySession session, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlaySession?> FindAsync(string code, CancellationToken cancellationToken) => Task.FromResult<PlaySession?>(code == Session.JoinCode ? Session : null);
        public Task<PlaySession?> UpdateAsync(string code, Action<PlaySession> update, CancellationToken cancellationToken)
        {
            Saved = false;
            if (code != Session.JoinCode) return Task.FromResult<PlaySession?>(null);
            update(Session);
            Saved = true;
            return Task.FromResult<PlaySession?>(Session);
        }
    }
    private sealed class RecordingNotifier(Store store) : IPlaySessionNotifier
    {
        public List<string> Codes { get; } = [];
        public Task ChangedAsync(string code) { Assert.True(store.Saved); Codes.Add(code); return Task.CompletedTask; }
    }
    private sealed class Caller : HubCallerContext
    {
        public override string ConnectionId => "connection";
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User => null;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => default;
        public override void Abort() { }
    }
    private sealed class Groups : IGroupManager
    {
        public List<string> Calls { get; } = [];
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        { Calls.Add($"+{connectionId}:{groupName}"); return Task.CompletedTask; }
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        { Calls.Add($"-{connectionId}:{groupName}"); return Task.CompletedTask; }
    }
    private sealed class Proxy : IClientProxy
    {
        public bool Fail { get; set; }
        public List<(string Method, object?[] Args)> Messages { get; } = [];
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            if (Fail) throw new InvalidOperationException("Disconnected");
            Messages.Add((method, args));
            return Task.CompletedTask;
        }
    }
    private sealed class RecordingClients : IHubClients
    {
        public Proxy Proxy { get; } = new();
        public List<string> Targets { get; } = [];
        public IClientProxy Group(string groupName) { Targets.Add(groupName); return Proxy; }
        public IClientProxy All => throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Client(string connectionId) => throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
        public IClientProxy User(string userId) => throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }
    private sealed class HubContext(RecordingClients clients) : IHubContext<PlayHub>
    {
        public IHubClients Clients => clients;
        public IGroupManager Groups { get; } = new Groups();
    }
}
