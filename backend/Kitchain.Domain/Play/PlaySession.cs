namespace Kitchain.Domain.Play;

public enum PlaySessionStatus { Draft, Active, Ended }
public enum PlayRotationMode { FairRotation }
public enum PlayScoringMode { Traditional }
public enum PlaySessionMode { QueueOnly, LiveScoring }

/// <summary>A session owns its participants and fairness state and chronological waiting-list tickets.</summary>
public sealed class PlaySession
{
    public const string JoinCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public const int JoinCodeLength = 6;
    private readonly List<PlaySessionPlayer> _players = [];
    private readonly List<PlayMatch> _matches = [];
    private PlaySession() { }

    public PlaySession(Guid id, string joinCode, string name, DateOnly sessionDate, TimeOnly startTime,
        TimeOnly endTime, int numberOfCourts, int? maximumPlayers, DateTimeOffset createdAt,
        PlaySessionMode mode = PlaySessionMode.QueueOnly, DateOnly? endDate = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("An ID is required.", nameof(id));
        var code = NormalizeCode(joinCode);
        if (code.Length != JoinCodeLength || code.Any(c => !JoinCodeAlphabet.Contains(c)))
            throw new ArgumentException("Use a six-character join code from the supported alphabet.", nameof(joinCode));
        SetDetails(name, sessionDate, startTime, endDate ?? sessionDate, endTime, numberOfCourts, maximumPlayers, mode);
        if (createdAt == default) throw new ArgumentException("A creation timestamp is required.", nameof(createdAt));
        Id = id;
        JoinCode = code;
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
    // The schedule uses local wall-clock dates/times; audit timestamps are UTC instants.
    public DateOnly SessionDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public int NumberOfCourts { get; private set; }
    public int? MaximumPlayers { get; private set; }
    public PlaySessionStatus Status { get; private set; }
    public PlaySessionMode Mode { get; private set; }
    public PlayRotationMode RotationMode { get; private set; }
    public PlayScoringMode ScoringMode { get; private set; }
    public int GameTo { get; private set; }
    public int WinBy { get; private set; }
    public long NextQueueOrder { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<PlaySessionPlayer> Players => _players.AsReadOnly();
    public IReadOnlyCollection<PlayMatch> Matches => _matches.AsReadOnly();
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
        player.EnterScheduling(Status == PlaySessionStatus.Active ? EntryBaseline() : 0);
        _players.Add(player);
        NextQueueOrder = player.QueueOrder!.Value;
        UpdatedAt = now.ToUniversalTime();
        FillFreeCourts(now);
        return player;
    }

    public void Rest(Guid playerId, DateTimeOffset now)
    {
        EnsureOpen(now);
        EnsureActivePlayers();
        FindPlayer(playerId).Rest(now);
        UpdatedAt = now.ToUniversalTime();
    }

    public void RenameGuest(Guid playerId, string displayName, DateTimeOffset now)
    {
        EnsureDraft(now);
        var player = FindPlayer(playerId);
        var normalized = PlaySessionPlayer.NormalizeName(displayName?.Trim() ?? "");
        if (_players.Any(p => p.Id != playerId && p.NormalizedDisplayName == normalized))
            throw new PlayConflictException("A player with this name is already in the session. Use a distinct name.");
        player.Rename(displayName!, now);
        UpdatedAt = now.ToUniversalTime();
    }

    public void RemoveGuest(Guid playerId, DateTimeOffset now)
    {
        EnsureDraft(now);
        _players.Remove(FindPlayer(playerId));
        UpdatedAt = now.ToUniversalTime();
    }

    private void EnsureDraft(DateTimeOffset now)
    {
        EnsureOpen(now);
        if (Status != PlaySessionStatus.Draft) throw new PlayConflictException("Only Draft session players can be edited or removed.");
    }

    public void Rejoin(Guid playerId, DateTimeOffset now)
    {
        EnsureOpen(now);
        EnsureActivePlayers();
        var player = FindPlayer(playerId);
        var ticket = NextTicket();
        var baseline = EntryBaseline();
        player.Rejoin(ticket, now);
        player.EnterScheduling(baseline);
        NextQueueOrder = ticket;
        UpdatedAt = now.ToUniversalTime();
        FillFreeCourts(now);
    }

    public void Start(DateTimeOffset now)
    {
        if (Status != PlaySessionStatus.Draft) throw new PlayConflictException("Only a Draft session can be started.");
        EnsureTimestamp(now);
        Status = PlaySessionStatus.Active;
        UpdatedAt = now.ToUniversalTime();
        FillFreeCourts(now);
    }

    public void FinishGame(Guid matchId, DateTimeOffset now)
    {
        EnsureOpen(now);
        if (Status != PlaySessionStatus.Active) throw new PlayConflictException("Only an Active session can finish a game.");
        var match = _matches.SingleOrDefault(m => m.Id == matchId && m.Status == PlayMatchStatus.Active)
            ?? throw new PlayConflictException("This game is no longer active.");
        match.Complete(now);
        UpdatedAt = now.ToUniversalTime();
    }

    public IReadOnlyList<PlaySessionPlayer> NextLineup(Guid matchId)
    {
        if (!_matches.Any(m => m.Id == matchId && m.IsCurrent && m.Status == PlayMatchStatus.Completed))
            throw new PlayConflictException("This court is no longer awaiting a next game.");
        return Recommend(EligibleNextPlayers(matchId), matchId);
    }

    public IReadOnlyList<PlaySessionPlayer> EligibleNextPlayers(Guid matchId)
    {
        var match = _matches.SingleOrDefault(m => m.Id == matchId && m.IsCurrent && m.Status == PlayMatchStatus.Completed)
            ?? throw new PlayConflictException("This court is no longer awaiting a next game.");
        if (Status != PlaySessionStatus.Active) return [];
        var returning = match.Players.Select(p => p.PlayerId).ToHashSet();
        var elsewhere = _matches.Where(m => m.IsCurrent && m.Id != matchId)
            .SelectMany(m => m.Players).Select(p => p.PlayerId).ToHashSet();
        return WaitingQueue.Concat(_players.Where(p => p.State == PlayPlayerState.Playing && returning.Contains(p.Id))
                .OrderBy(p => p.JoinedAt).ThenBy(p => p.Id))
            .Where(p => !elsewhere.Contains(p.Id)).ToArray();
    }

    public void StartNextGame(Guid matchId, IReadOnlyList<Guid> playerIds, bool overrideLineup, DateTimeOffset now)
    {
        EnsureOpen(now);
        if (Status != PlaySessionStatus.Active) throw new PlayConflictException("Only an Active session can start a next game.");
        var match = _matches.SingleOrDefault(m => m.Id == matchId && m.IsCurrent && m.Status == PlayMatchStatus.Completed)
            ?? throw new PlayConflictException("This court is no longer awaiting a next game.");
        if (playerIds.Count != 4 || playerIds.Distinct().Count() != 4)
            throw new ArgumentException("Choose exactly four distinct eligible players.", nameof(playerIds));
        var eligible = EligibleNextPlayers(matchId).ToDictionary(p => p.Id);
        if (playerIds.Any(id => !eligible.ContainsKey(id)))
            throw new PlayConflictException("The next lineup has changed. Refresh and choose four eligible players.");
        if (!overrideLineup && !playerIds.SequenceEqual(NextLineup(matchId).Select(p => p.Id)))
            throw new PlayConflictException("The fair lineup has changed. Refresh before starting the next game.");
        var selected = playerIds.Select(id => eligible[id]).ToArray();
        var returning = match.Players.OrderBy(p => p.Position).Select(p => FindPlayer(p.PlayerId)).ToArray();
        if (returning.Length != 4 || returning.Any(p => p.State != PlayPlayerState.Playing))
            throw new PlayConflictException("The game participants have changed. Refresh the session.");
        if (NextQueueOrder > long.MaxValue - 4)
            throw new PlayConflictException("This session cannot allocate another queue position.");
        ValidateOpportunity(eligible.Values);
        // All eligibility, lifecycle and ticket checks precede any player movement.
        // Continuing players receive a return ticket too, then immediately leave the queue.
        foreach (var player in returning) player.Finish(++NextQueueOrder, now);
        var next = new PlayMatch(Id, match.CourtNumber, selected, now);
        match.ReleaseCourt();
        foreach (var player in selected) player.Play(now);
        RecordOpportunity(eligible.Values, playerIds);
        _matches.Add(next);
        UpdatedAt = now.ToUniversalTime();
    }

    public void RecordRally(Guid matchId, PlayTeam winner, DateTimeOffset now)
    {
        var match = ScoringMatch(matchId, now);
        var won = match.RecordRally(winner, GameTo, WinBy, now);
        UpdatedAt = now.ToUniversalTime();
        if (won) match.Complete(now, winner);
    }

    public void EditRallyCallOut(Guid matchId, Guid rallyId, PlayRallyCallOut? callOut,
        PlayRallyCallOut? expectedCallOut, DateTimeOffset now)
    {
        EnsureOpen(now);
        if (Status != PlaySessionStatus.Active) throw new PlayConflictException("Only an Active session can be scored.");
        if (Mode != PlaySessionMode.LiveScoring) throw new PlayConflictException("Live scoring is not enabled for this session.");
        // Allow annotation of a winning/final rally while its completed game is held.
        // Historical matches and Ended sessions remain read-only in this phase.
        var match = _matches.SingleOrDefault(m => m.Id == matchId && m.IsCurrent)
            ?? throw new PlayConflictException("This game is no longer current. Refresh the session.");
        match.EditCallOut(rallyId, callOut, expectedCallOut);
        UpdatedAt = now.ToUniversalTime();
    }

    public void CorrectScore(Guid matchId, int teamAScore, int teamBScore, PlayTeam servingTeam,
        int currentServerNumber, DateTimeOffset now)
    {
        var match = ScoringMatch(matchId, now);
        match.CorrectScore(teamAScore, teamBScore, servingTeam, currentServerNumber);
        if (teamAScore >= GameTo && teamAScore - teamBScore >= WinBy) match.Complete(now, PlayTeam.A);
        else if (teamBScore >= GameTo && teamBScore - teamAScore >= WinBy) match.Complete(now, PlayTeam.B);
        UpdatedAt = now.ToUniversalTime();
    }

    private PlayMatch ScoringMatch(Guid matchId, DateTimeOffset now)
    {
        EnsureOpen(now);
        if (Status != PlaySessionStatus.Active) throw new PlayConflictException("Only an Active session can be scored.");
        if (Mode != PlaySessionMode.LiveScoring) throw new PlayConflictException("Live scoring is not enabled for this session.");
        return _matches.SingleOrDefault(m => m.Id == matchId && m.Status == PlayMatchStatus.Active)
            ?? throw new PlayConflictException("This game is no longer active.");
    }

    private IReadOnlyList<PlaySessionPlayer> Recommend(IReadOnlyList<PlaySessionPlayer> eligible, Guid seed) =>
        PlayTeamPairing.Recommend(Id, PlayFairRotation.Select(eligible, _matches, seed), _matches, seed);

    private void FillFreeCourts(DateTimeOffset now)
    {
        if (Status != PlaySessionStatus.Active) return;
        var occupied = _matches.Where(m => m.IsCurrent).Select(m => m.CourtNumber).ToHashSet();
        for (long court = 1; court <= NumberOfCourts && WaitingQueue.Count >= 4; court++)
        {
            if (occupied.Contains((int)court)) continue;
            var eligible = WaitingQueue;
            ValidateOpportunity(eligible);
            var players = Recommend(eligible, Id);
            var match = new PlayMatch(Id, (int)court, players, now);
            foreach (var player in players) player.Play(now);
            RecordOpportunity(eligible, players.Select(p => p.Id).ToArray());
            _matches.Add(match);
        }
    }

    private static void ValidateOpportunity(IEnumerable<PlaySessionPlayer> eligible)
    {
        if (eligible.Any(p => p.AdjustedGamesStarted == long.MaxValue || p.MissedOpportunities == long.MaxValue))
            throw new PlayConflictException("This session cannot allocate another playing opportunity.");
    }

    private static void RecordOpportunity(IEnumerable<PlaySessionPlayer> eligible, IReadOnlyList<Guid> selected)
    {
        foreach (var player in eligible) player.RecordOpportunity(selected.Contains(player.Id));
    }

    private long EntryBaseline()
    {
        var values = _players.Where(p => p.State != PlayPlayerState.Resting)
            .Select(p => p.AdjustedGamesStarted).Order().ToArray();
        return values.Length == 0 ? 0 : values[(values.Length - 1) / 2];
    }

    private void EnsureActivePlayers()
    {
        if (Status != PlaySessionStatus.Active)
            throw new PlayConflictException("Only an Active session supports taking a break or rejoining.");
    }

    public void EditDetails(string name, DateOnly startDate, TimeOnly startTime, DateOnly endDate,
        TimeOnly endTime, int numberOfCourts, int? maximumPlayers, PlaySessionMode mode, DateTimeOffset now)
    {
        EnsureOpen(now);
        if (Status != PlaySessionStatus.Draft) throw new PlayConflictException("Only a Draft session can edit its details.");
        SetDetails(name, startDate, startTime, endDate, endTime, numberOfCourts, maximumPlayers, mode);
        UpdatedAt = now.ToUniversalTime();
    }

    private void SetDetails(string name, DateOnly startDate, TimeOnly startTime, DateOnly endDate,
        TimeOnly endTime, int numberOfCourts, int? maximumPlayers, PlaySessionMode mode)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrEmpty(normalized) || normalized.Length > 200)
            throw new ArgumentException("Supply a session name of 1–200 characters.", nameof(name));
        if (startDate == default) throw new ArgumentException("A start date is required.", "date");
        if (endDate == default || endDate.ToDateTime(endTime) <= startDate.ToDateTime(startTime))
            throw new ArgumentException("End date and time must follow start date and time.", nameof(endTime));
        if (numberOfCourts <= 0) throw new ArgumentException("Number of courts must be positive.", nameof(numberOfCourts));
        if (maximumPlayers is <= 0) throw new ArgumentException("Maximum players must be positive.", nameof(maximumPlayers));
        if (maximumPlayers.HasValue && maximumPlayers.Value < _players.Count)
            throw new PlayConflictException("Maximum players cannot be lower than the current roster size.");
        if (!Enum.IsDefined(mode)) throw new ArgumentException("Choose QueueOnly or LiveScoring.", nameof(mode));
        Name = normalized;
        SessionDate = startDate;
        StartTime = startTime;
        EndDate = endDate;
        EndTime = endTime;
        NumberOfCourts = numberOfCourts;
        MaximumPlayers = maximumPlayers;
        Mode = mode;
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
