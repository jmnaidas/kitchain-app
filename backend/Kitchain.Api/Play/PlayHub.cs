using Kitchain.Domain.Play;
using Microsoft.AspNetCore.SignalR;

namespace Kitchain.Api.Play;

public sealed class PlayHub : Hub
{
    public Task JoinSessionGroup(string code) =>
        Groups.AddToGroupAsync(Context.ConnectionId, Group(code), Context.ConnectionAborted);

    public Task LeaveSessionGroup(string code) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(code), Context.ConnectionAborted);

    internal static string Group(string code)
    {
        var normalized = PlaySession.NormalizeCode(code);
        if (normalized.Length != PlaySession.JoinCodeLength ||
            normalized.Any(c => !PlaySession.JoinCodeAlphabet.Contains(c)))
            throw new HubException("Enter a valid session code.");
        return $"Session:{normalized}";
    }
}

public interface IPlaySessionNotifier
{
    Task ChangedAsync(string code);
}

public sealed record PlaySessionChanged(string Code);

public sealed class SignalRPlaySessionNotifier(IHubContext<PlayHub> hub, ILogger<SignalRPlaySessionNotifier> logger)
    : IPlaySessionNotifier
{
    public async Task ChangedAsync(string code)
    {
        // The REST operation has committed. A failed notification must not turn it into a failed mutation.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            var normalized = PlaySession.NormalizeCode(code);
            await hub.Clients.Group(PlayHub.Group(normalized))
                .SendAsync("SessionChanged", new PlaySessionChanged(normalized), timeout.Token);
        }
        catch (Exception error)
        {
            logger.LogWarning(error, "Could not notify Play session clients after a committed change.");
        }
    }
}
