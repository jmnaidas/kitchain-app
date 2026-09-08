namespace Kitchain.Domain.Play;

public enum PlaySessionStatus { Draft, Active, Ended }
public enum PlayRotationMode { FairRotation }
public enum PlayScoringMode { Traditional }

/// <summary>A session owns its participants and monotonically increasing FIFO queue tickets.</summary>
public sealed class PlaySession
{
    public const string JoinCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public const int JoinCodeLength = 6;
    private readonly List<PlaySessionPlayer> _players = [];
    private PlaySession() { }

    public PlaySession(Guid id, string joinCode, string name, DateOnly sessionDate, TimeOnly startTime,
        TimeOnly endTime, int numberOfCourts, int? maximumPlayers, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("An ID is required.", nameof(id));
        var code = NormalizeCode(joinCode);
        if (code.Length != JoinCodeLength || code.Any(c => !JoinCodeAlphabet.Contains(c)))
            throw new ArgumentException("Use a six-character join code from the supported alphabet.", nameof(joinCode));
        var normalizedName = name?.Trim();
        if (string.IsNullOrEmpty(normalizedName) || normalizedName.Length > 200)
            throw new ArgumentException("Supply a session name of 1–200 characters.", nameof(name));
        if (sessionDate == default) throw new ArgumentException("A session date is required.", nameof(sessionDate));
        if (endTime <= startTime)
            throw new ArgumentException("End time must follow start time on the same session date.", nameof(endTime));
        if (numberOfCourts <= 0) throw new ArgumentException("Number of courts must be positive.", nameof(numberOfCourts));
        if (maximumPlayers is <= 0) throw new ArgumentException("Maximum players must be positive when supplied.", nameof(maximumPlayers));
        if (createdAt == default) throw new ArgumentException("A creation timestamp is required.", nameof(createdAt));
        Id = id;
        JoinCode = code;
        Name = normalizedName;
        SessionDate = sessionDate;
        StartTime = startTime;
        EndTime = endTime;
        NumberOfCourts = numberOfCourts;
        MaximumPlayers = maximumPlayers;
        Status = PlaySessionStatus.Draft;
        RotationMode = PlayRotationMode.FairRotation;
        ScoringMode = PlayScoringMode.Traditional;
        GameTo = 11;
        WinBy = 2;
        CreatedAt = createdAt.ToUniversalTime();
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }
    public string JoinCode { get; private set; } = "";
    public string Name { get; private set; } = "";
    // The schedule is a same-day wall-clock date/time; audit timestamps are UTC instants.
    public DateOnly SessionDate { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public int NumberOfCourts { get; private set; }
    public int? MaximumPlayers { get; private set; }
    public PlaySessionStatus Status { get; private set; }
    public PlayRotationMode RotationMode { get; private set; }
    public PlayScoringMode ScoringMode { get; private set; }
    public int GameTo { get; private set; }
    public int WinBy { get; private set; }
    public long NextQueueOrder { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<PlaySessionPlayer> Players => _players.AsReadOnly();
    public IReadOnlyList<PlaySessionPlayer> WaitingQueue => _players.Where(p => p.State == PlayPlayerState.Waiting)
        .OrderBy(p => p.QueueOrder).ThenBy(p => p.Id).ToArray();

    public static string NormalizeCode(string? code) => code?.Trim().ToUpperInvariant() ?? "";

    public PlaySessionPlayer AddGuest(string displayName, DateTimeOffset now)
    {
        EnsureOpen(now);
        // Resting participants retain their place in the session's capacity, not in its queue.
        if (MaximumPlayers.HasValue && _players.Count >= MaximumPlayers.Value)
            throw new PlayConflictException("This session has reached its player limit.");
        var player = new PlaySessionPlayer(Guid.NewGuid(), Id, displayName, now, NextTicket());
        if (_players.Any(p => p.NormalizedDisplayName == player.NormalizedDisplayName))
            throw new PlayConflictException("A player with this name is already in the session. Use a distinct name.");
        _players.Add(player);
        NextQueueOrder = player.QueueOrder!.Value;
        UpdatedAt = now.ToUniversalTime();
        return player;
    }

    public void Rest(Guid playerId, DateTimeOffset now)
    {
        EnsureOpen(now);
        FindPlayer(playerId).Rest(now);
        UpdatedAt = now.ToUniversalTime();
    }

    public void Rejoin(Guid playerId, DateTimeOffset now)
    {
        EnsureOpen(now);
        var player = FindPlayer(playerId);
        var ticket = NextTicket();
        player.Rejoin(ticket, now);
        NextQueueOrder = ticket;
        UpdatedAt = now.ToUniversalTime();
    }

    public void Start(DateTimeOffset now)
    {
        if (Status != PlaySessionStatus.Draft) throw new PlayConflictException("Only a Draft session can be started.");
        EnsureTimestamp(now);
        Status = PlaySessionStatus.Active;
        UpdatedAt = now.ToUniversalTime();
    }

    public void End(DateTimeOffset now)
    {
        if (Status != PlaySessionStatus.Active) throw new PlayConflictException("Only an Active session can be ended.");
        EnsureTimestamp(now);
        Status = PlaySessionStatus.Ended;
        UpdatedAt = now.ToUniversalTime();
    }

    private PlaySessionPlayer FindPlayer(Guid id) => _players.SingleOrDefault(p => p.Id == id)
        ?? throw new KeyNotFoundException("Player not found in this session.");

    private long NextTicket() => NextQueueOrder < long.MaxValue ? NextQueueOrder + 1
        : throw new PlayConflictException("This session cannot allocate another queue position.");

    private void EnsureOpen(DateTimeOffset now)
    {
        if (Status == PlaySessionStatus.Ended) throw new PlayConflictException("This session has ended.");
        EnsureTimestamp(now);
    }

    private void EnsureTimestamp(DateTimeOffset now)
    {
        if (now == default || now < UpdatedAt)
            throw new ArgumentException("The update timestamp cannot precede the last session update.", nameof(now));
    }
}

public sealed class PlayConflictException(string message) : InvalidOperationException(message);
