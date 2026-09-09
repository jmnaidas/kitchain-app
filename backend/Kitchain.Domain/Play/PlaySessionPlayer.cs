namespace Kitchain.Domain.Play;

public enum PlayPlayerIdentityType { Guest }
public enum PlayPlayerState { Waiting, Playing, Resting }

public sealed class PlaySessionPlayer
{
    private PlaySessionPlayer() { }

    internal PlaySessionPlayer(Guid id, Guid sessionId, string displayName, DateTimeOffset joinedAt, long queueOrder)
    {
        var name = displayName?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 80)
            throw new ArgumentException("Supply a player name of 1–80 characters.", nameof(displayName));
        Id = id;
        SessionId = sessionId;
        DisplayName = name;
        // Case and repeated whitespace cannot disguise an existing participant's name.
        NormalizedDisplayName = string.Join(" ", name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
        IdentityType = PlayPlayerIdentityType.Guest;
        State = PlayPlayerState.Waiting;
        JoinedAt = joinedAt.ToUniversalTime();
        UpdatedAt = JoinedAt;
        QueueOrder = queueOrder;
    }

    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public string DisplayName { get; private set; } = "";
    public string NormalizedDisplayName { get; private set; } = "";
    public PlayPlayerIdentityType IdentityType { get; private set; }
    public PlayPlayerState State { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long? QueueOrder { get; private set; }

    internal void Play(DateTimeOffset now)
    {
        if (State != PlayPlayerState.Waiting) throw new PlayConflictException("Only a Waiting player can play.");
        State = PlayPlayerState.Playing;
        QueueOrder = null;
        UpdatedAt = now.ToUniversalTime();
    }

    internal void Finish(long queueOrder, DateTimeOffset now)
    {
        if (State != PlayPlayerState.Playing) throw new PlayConflictException("Only a Playing player can finish a game.");
        State = PlayPlayerState.Waiting;
        QueueOrder = queueOrder;
        UpdatedAt = now.ToUniversalTime();
    }

    internal void Rest(DateTimeOffset now)
    {
        if (State != PlayPlayerState.Waiting) throw new PlayConflictException("Only a Waiting player can take a break.");
        State = PlayPlayerState.Resting;
        QueueOrder = null;
        UpdatedAt = now.ToUniversalTime();
    }

    internal void Rejoin(long queueOrder, DateTimeOffset now)
    {
        if (State != PlayPlayerState.Resting) throw new PlayConflictException("Only a Resting player can rejoin the queue.");
        State = PlayPlayerState.Waiting;
        QueueOrder = queueOrder;
        UpdatedAt = now.ToUniversalTime();
    }
}
