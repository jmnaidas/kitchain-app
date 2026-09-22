namespace Kitchain.Domain.Play;

public enum PlaySessionStatus { Draft, Active, Ended }
public enum PlayRotationMode { FairRotation, WinnersStay, ChallengersStay, SplitTeams }
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
        PlaySessionMode mode = PlaySessionMode.QueueOnly, DateOnly? endDate = null,
        PlayRotationMode rotationMode = PlayRotationMode.FairRotation)
    {
        if (id == Guid.Empty) throw new ArgumentException("An ID is required.", nameof(id));
        var code = NormalizeCode(joinCode);
        if (code.Length != JoinCodeLength || code.Any(c => !JoinCodeAlphabet.Contains(c)))
            throw new ArgumentException("Use a six-character join code from the supported alphabet.", nameof(joinCode));
        SetDetails(name, sessionDate, startTime, endDate ?? sessionDate, endTime, numberOfCourts, maximumPlayers, mode, rotationMode);
        if (createdAt == default) throw new ArgumentException("A creation timestamp is required.", nameof(createdAt));
        Id = id;
        JoinCode = code;
        Status = PlaySessionStatus.Draft;
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
    public IReadOnlyList<PlaySessionPlayer> WaitingQueue => _players.Where(p => p.State == PlayPlayerState.Waiting &&
        !_matches.Any(m => m.IsCurrent && m.Status == PlayMatchStatus.Ready && m.Players.Any(slot => slot.PlayerId == p.Id)))
        .OrderBy(p => p.QueueOrder).ThenBy(p => p.Id).ToArray();

    public static string NormalizeCode(string? code) => code?.Trim().ToUpperInvariant() ?? "";

    public PlaySessionPlayer AddGuest(string displayName, DateTimeOffset now)
    {
        EnsureOpen(now);
        // Resting participants retain their place in the session's capacity, not in its queue.
        if (MaximumPlayers.HasValue && _players.Count(p => !p.IsRemoved) >= MaximumPlayers.Value)
            throw new PlayConflictException("This session has reached its player limit.");
        var player = new PlaySessionPlayer(Guid.NewGuid(), Id, displayName, now, NextTicket());
        if (_players.Any(p => !p.IsRemoved && p.NormalizedDisplayName == player.NormalizedDisplayName))
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
        EnsureNotReserved(playerId);
        FindPlayer(playerId).Rest(now);
        UpdatedAt = now.ToUniversalTime();
    }

    public void RenameGuest(Guid playerId, string displayName, DateTimeOffset now)
    {
        EnsureOpen(now);
        var player = FindPlayer(playerId);
        var normalized = PlaySessionPlayer.NormalizeName(displayName?.Trim() ?? "");
        if (_players.Any(p => !p.IsRemoved && p.Id != playerId && p.NormalizedDisplayName == normalized))
            throw new PlayConflictException("A player with this name is already in the session. Use a distinct name.");
        player.Rename(displayName!, now);
        // Active games follow roster names; completed participant snapshots never change.
        foreach (var slot in _matches.Where(m => m.Status is PlayMatchStatus.Active or PlayMatchStatus.Ready)
            .SelectMany(m => m.Players).Where(p => p.PlayerId == playerId))
            slot.Rename(player.DisplayName);
        UpdatedAt = now.ToUniversalTime();
    }

    public void RemoveGuest(Guid playerId, DateTimeOffset now)
    {
        EnsureOpen(now);
        EnsureNotReserved(playerId);
        var player = FindPlayer(playerId);
        player.Remove(now);
        if (!_matches.Any(m => m.Players.Any(p => p.PlayerId == playerId))) _players.Remove(player);
        UpdatedAt = now.ToUniversalTime();
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

    public void FinishGame(Guid matchId, DateTimeOffset now, PlayTeam? winner = null)
    {
        EnsureOpen(now);
        if (Status != PlaySessionStatus.Active) throw new PlayConflictException("Only an Active session can finish a game.");
        var match = _matches.SingleOrDefault(m => m.Id == matchId && m.Status == PlayMatchStatus.Active)
            ?? throw new PlayConflictException("This game is no longer active.");
        if (winner.HasValue && !Enum.IsDefined(winner.Value))
            throw new ArgumentException("Choose winning team A or B.", nameof(winner));
        if (winner.HasValue && Mode != PlaySessionMode.QueueOnly)
            throw new PlayConflictException("Only Queue Only games can record a result when finishing manually.");
        match.Complete(now, winner);
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
            .Where(p => !p.IsRemoved && !elsewhere.Contains(p.Id)).ToArray();
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
        // All eligibility, lifecycle and ticket checks precede any player movement.
        // Continuing players receive a return ticket too, then immediately leave the queue.
        foreach (var player in returning) player.Finish(++NextQueueOrder, now);
        var next = new PlayMatch(Id, match.CourtNumber, selected, now);
        if (overrideLineup) next.SetLineup(selected, true);
        match.ReleaseCourt();
        _matches.Add(next);
        UpdatedAt = now.ToUniversalTime();
    }

    public IReadOnlyList<PlaySessionPlayer> EligibleReadyPlayers(Guid matchId)
    {
        var match = ReadyMatch(matchId);
        var own = match.Players.Select(p => p.PlayerId).ToHashSet();
        return WaitingQueue.Concat(_players.Where(p => own.Contains(p.Id) && p.State == PlayPlayerState.Waiting && !p.IsRemoved)).ToArray();
    }

    public IReadOnlyList<PlaySessionPlayer> ReadyRecommendation(Guid matchId)
    {
        var match = ReadyMatch(matchId);
        var previous = _matches.Where(m => m.CourtNumber == match.CourtNumber && m.Status == PlayMatchStatus.Completed)
            .OrderByDescending(m => m.CompletedAt).ThenByDescending(m => m.StartedAt).FirstOrDefault();
        return Recommend(EligibleReadyPlayers(matchId), previous?.Id ?? Id);
    }

    public void ChangeLineupSlot(Guid matchId, int position, Guid playerId, long expectedRevision, DateTimeOffset now, Guid? otherMatchId = null, long? otherExpectedRevision = null)
    {
        EnsureOpen(now);
        var match = ReadyMatch(matchId);
        match.CheckReady(expectedRevision);
        if (position is < 1 or > 4) throw new ArgumentException("Choose a position from 1 to 4.", nameof(position));
        var assigned = _matches.SingleOrDefault(m => m.IsCurrent && m.Id != matchId &&
            m.Players.Any(p => p.PlayerId == playerId));
        if (assigned is not null || otherMatchId is not null)
        {
            if (assigned is null || assigned.Id != otherMatchId || otherExpectedRevision is null)
                throw new PlayConflictException("The other lineup changed. Refresh before swapping courts.");
            assigned.CheckReady(otherExpectedRevision.Value);
            var first = match.Players.OrderBy(p => p.Position).Select(p => FindPlayer(p.PlayerId)).ToArray();
            var second = assigned.Players.OrderBy(p => p.Position).Select(p => FindPlayer(p.PlayerId)).ToArray();
            if (first.Length != 4 || second.Length != 4 ||
                first.Concat(second).Select(p => p.Id).Distinct().Count() != 8 ||
                first.Concat(second).Any(p => p.IsRemoved || p.State != PlayPlayerState.Waiting))
                throw new PlayConflictException("Both courts require four eligible proposed players.");
            var target = Array.FindIndex(second, p => p.Id == playerId);
            (first[position - 1], second[target]) = (second[target], first[position - 1]);
            // Validate both revisions and lineups before changing either; the store locks this session.
            match.SetLineup(first, true);
            assigned.SetLineup(second, true);
            UpdatedAt = now.ToUniversalTime();
            return;
        }
        var eligible = EligibleReadyPlayers(matchId).ToDictionary(p => p.Id);
        if (!eligible.ContainsKey(playerId)) throw new PlayConflictException("That player is unavailable or assigned to another court.");
        var ids = match.Players.OrderBy(p => p.Position).Select(p => p.PlayerId).ToArray();
        var other = Array.IndexOf(ids, playerId);
        if (other == position - 1) return;
        if (other >= 0) ids[other] = ids[position - 1];
        ids[position - 1] = playerId;
        match.SetLineup(ids.Select(id => eligible[id]).ToArray(), true);
        UpdatedAt = now.ToUniversalTime();
    }

    public void ResetRecommendation(Guid matchId, long expectedRevision, DateTimeOffset now)
    {
        EnsureOpen(now);
        var match = ReadyMatch(matchId);
        match.CheckReady(expectedRevision);
        match.SetLineup(ReadyRecommendation(matchId), false);
        UpdatedAt = now.ToUniversalTime();
    }

    public void StartGame(Guid matchId, long expectedRevision, DateTimeOffset now)
    {
        EnsureOpen(now);
        var match = ReadyMatch(matchId);
        match.CheckReady(expectedRevision);
        var eligible = EligibleReadyPlayers(matchId);
        var ids = match.Players.OrderBy(p => p.Position).Select(p => p.PlayerId).ToArray();
        if (ids.Length != 4 || ids.Distinct().Count() != 4 || ids.Any(id => !eligible.Any(p => p.Id == id)))
            throw new PlayConflictException("The lineup is no longer eligible. Refresh before starting.");
        ValidateOpportunity(eligible);
        // Reservations never count as participation. Only this final started lineup does.
        match.Start(now);
        foreach (var id in ids) FindPlayer(id).Play(now);
        RecordOpportunity(eligible, ids);
        UpdatedAt = now.ToUniversalTime();
    }

    private PlayMatch ReadyMatch(Guid id)
    {
        if (Status != PlaySessionStatus.Active) throw new PlayConflictException("Only an Active session can prepare a game.");
        return _matches.SingleOrDefault(m => m.Id == id && m.IsCurrent && m.Status == PlayMatchStatus.Ready)
            ?? throw new PlayConflictException("This court is no longer Ready. Refresh the session.");
    }

    private void EnsureNotReserved(Guid playerId)
    {
        if (_matches.Any(m => m.IsCurrent && m.Status == PlayMatchStatus.Ready && m.Players.Any(p => p.PlayerId == playerId)))
            throw new PlayConflictException("Replace this player in the Ready lineup before sitting out or removing them.");
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
        PlayRotationPolicy.Recommend(Id, RotationMode, eligible, _matches, seed);

    // A preview only: no opportunity counters or tickets are changed by a read.
    public (IReadOnlyList<PlaySessionPlayer> Next, IReadOnlyList<PlaySessionPlayer> Waiting,
        int Needed, int Held, int? Court) RotationPreview()
    {
        var completed = Status == PlaySessionStatus.Active
            ? _matches.Where(m => m.IsCurrent && m.Status == PlayMatchStatus.Completed)
                .OrderBy(m => m.CompletedAt).ThenBy(m => m.CourtNumber).FirstOrDefault() : null;
        var eligible = completed is null ? WaitingQueue : EligibleNextPlayers(completed.Id);
        IReadOnlyList<PlaySessionPlayer> selected = Status != PlaySessionStatus.Ended && eligible.Count >= 4
            ? Recommend(eligible, completed?.Id ?? Id) : [];
        var ready = Status == PlaySessionStatus.Active
            ? _matches.Where(m => m.IsCurrent && m.Status == PlayMatchStatus.Ready).OrderBy(m => m.CourtNumber)
                .SelectMany(m => m.Players.OrderBy(p => p.Position)).Select(p => FindPlayer(p.PlayerId)).ToArray() : [];
        var next = ready.Concat(selected.Where(p => p.State == PlayPlayerState.Waiting)).DistinctBy(p => p.Id).ToArray();
        var ids = next.Select(p => p.Id).ToHashSet();
        var waiting = WaitingQueue.Where(p => !ids.Contains(p.Id))
            .OrderBy(p => p.AdjustedGamesStarted).ThenByDescending(p => p.MissedOpportunities / 2)
            .ThenBy(p => p.WaitingSince).ThenBy(p => p.QueueOrder).ThenBy(p => p.Id).ToArray();
        return (next, waiting, Math.Max(0, 4 - eligible.Count),
            selected.Count(p => p.State == PlayPlayerState.Playing), completed?.CourtNumber);
    }

    private void FillFreeCourts(DateTimeOffset now)
    {
        if (Status != PlaySessionStatus.Active) return;
        var occupied = _matches.Where(m => m.IsCurrent).Select(m => m.CourtNumber).ToHashSet();
        for (long court = 1; court <= NumberOfCourts && WaitingQueue.Count >= 4; court++)
        {
            if (occupied.Contains((int)court)) continue;
            var eligible = WaitingQueue;
            var players = Recommend(eligible, Id);
            var match = new PlayMatch(Id, (int)court, players, now);
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
        var values = _players.Where(p => !p.IsRemoved && p.State != PlayPlayerState.Resting)
            .Select(p => p.AdjustedGamesStarted).Order().ToArray();
        return values.Length == 0 ? 0 : values[(values.Length - 1) / 2];
    }

    private void EnsureActivePlayers()
    {
        if (Status != PlaySessionStatus.Active)
            throw new PlayConflictException("Only an Active session supports taking a break or rejoining.");
    }

    public void EditDetails(string name, DateOnly startDate, TimeOnly startTime, DateOnly endDate,
        TimeOnly endTime, int numberOfCourts, int? maximumPlayers, PlaySessionMode mode, DateTimeOffset now,
        PlayRotationMode? rotationMode = null)
    {
        EnsureOpen(now);
        if (Status != PlaySessionStatus.Draft) throw new PlayConflictException("Only a Draft session can edit its details.");
        SetDetails(name, startDate, startTime, endDate, endTime, numberOfCourts, maximumPlayers, mode, rotationMode ?? RotationMode);
        UpdatedAt = now.ToUniversalTime();
    }

    private void SetDetails(string name, DateOnly startDate, TimeOnly startTime, DateOnly endDate,
        TimeOnly endTime, int numberOfCourts, int? maximumPlayers, PlaySessionMode mode, PlayRotationMode rotationMode)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrEmpty(normalized) || normalized.Length > 200)
            throw new ArgumentException("Supply a session name of 1–200 characters.", nameof(name));
        if (startDate == default) throw new ArgumentException("A start date is required.", "date");
        if (endDate == default || endDate.ToDateTime(endTime) <= startDate.ToDateTime(startTime))
            throw new ArgumentException("End date and time must follow start date and time.", nameof(endTime));
        if (numberOfCourts <= 0) throw new ArgumentException("Number of courts must be positive.", nameof(numberOfCourts));
        if (maximumPlayers is <= 0) throw new ArgumentException("Maximum players must be positive.", nameof(maximumPlayers));
        if (maximumPlayers.HasValue && maximumPlayers.Value < _players.Count(p => !p.IsRemoved))
            throw new PlayConflictException("Maximum players cannot be lower than the current roster size.");
        if (!Enum.IsDefined(mode)) throw new ArgumentException("Choose QueueOnly or LiveScoring.", nameof(mode));
        if (!Enum.IsDefined(rotationMode)) throw new ArgumentException("Choose a supported rotation style.", nameof(rotationMode));
        RotationMode = rotationMode;
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

    private PlaySessionPlayer FindPlayer(Guid id) => _players.SingleOrDefault(p => p.Id == id && !p.IsRemoved)
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
