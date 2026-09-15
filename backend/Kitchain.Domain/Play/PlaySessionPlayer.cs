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
        NormalizedDisplayName = NormalizeName(name);
        IdentityType = PlayPlayerIdentityType.Guest;
        State = PlayPlayerState.Waiting;
        JoinedAt = joinedAt.ToUniversalTime();
        UpdatedAt = JoinedAt;
        QueueOrder = queueOrder;
        WaitingSince = JoinedAt;
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

    public DateTimeOffset? WaitingSince { get; private set; }
    public long AdjustedGamesStarted { get; private set; }
    public long MissedOpportunities { get; private set; }

    internal void EnterScheduling(long baseline)
    {
        AdjustedGamesStarted = Math.Max(AdjustedGamesStarted, baseline);
        MissedOpportunities = 0;
    }

    internal void RecordOpportunity(bool selected)
    {
        if (selected) { AdjustedGamesStarted++; MissedOpportunities = 0; }
        else MissedOpportunities++;
    }

    internal void Play(DateTimeOffset now)
    {
        if (State != PlayPlayerState.Waiting) throw new PlayConflictException("Only a Waiting player can play.");
        State = PlayPlayerState.Playing;
        QueueOrder = null;
        WaitingSince = null;
        UpdatedAt = now.ToUniversalTime();
    }

    internal void Rename(string displayName, DateTimeOffset now)
    {
        var name = displayName?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 80)
            throw new ArgumentException("Supply a player name of 1–80 characters.", nameof(displayName));
        DisplayName = name;
        NormalizedDisplayName = NormalizeName(name);
        UpdatedAt = now.ToUniversalTime();
    }

    internal static string NormalizeName(string name) =>
        string.Join(" ", name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    internal void Finish(long queueOrder, DateTimeOffset now)
    {
        if (State != PlayPlayerState.Playing) throw new PlayConflictException("Only a Playing player can finish a game.");
        State = PlayPlayerState.Waiting;
        QueueOrder = queueOrder;
        WaitingSince = now.ToUniversalTime();
        UpdatedAt = now.ToUniversalTime();
    }

    internal void Rest(DateTimeOffset now)
    {
        if (State != PlayPlayerState.Waiting) throw new PlayConflictException("Only a Waiting player can take a break.");
        State = PlayPlayerState.Resting;
        MissedOpportunities = 0;
        QueueOrder = null;
        WaitingSince = null;
        UpdatedAt = now.ToUniversalTime();
    }

    internal void Rejoin(long queueOrder, DateTimeOffset now)
    {
        if (State != PlayPlayerState.Resting) throw new PlayConflictException("Only a Resting player can rejoin the queue.");
        State = PlayPlayerState.Waiting;
        QueueOrder = queueOrder;
        WaitingSince = now.ToUniversalTime();
        UpdatedAt = now.ToUniversalTime();
    }
}
