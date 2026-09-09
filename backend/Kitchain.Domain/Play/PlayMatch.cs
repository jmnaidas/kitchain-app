namespace Kitchain.Domain.Play;

public enum PlayMatchStatus { Active, Completed }
public enum PlayTeam { A, B }

public sealed class PlayMatch
{
    private readonly List<PlayMatchPlayer> _players = [];
    private PlayMatch() { }

    internal PlayMatch(Guid sessionId, int courtNumber, IReadOnlyList<PlaySessionPlayer> players, DateTimeOffset now)
    {
        if (players.Count != 4 || players.Select(p => p.Id).Distinct().Count() != 4 ||
            players.Any(p => p.SessionId != sessionId || p.State != PlayPlayerState.Waiting))
            throw new PlayConflictException("A game requires four distinct waiting players from this session.");
        Id = Guid.NewGuid();
        SessionId = sessionId;
        CourtNumber = courtNumber;
        Status = PlayMatchStatus.Active;
        StartedAt = now.ToUniversalTime();
        for (var i = 0; i < 4; i++)
            _players.Add(new PlayMatchPlayer(Id, players[i].Id, i + 1));
    }

    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public int CourtNumber { get; private set; }
    public PlayMatchStatus Status { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public int TeamAScore { get; private set; }
    public int TeamBScore { get; private set; }
    public PlayTeam ServingTeam { get; private set; } = PlayTeam.A;
    // Opening doubles service has only one entitlement: announce 0-0-2.
    public int CurrentServerNumber { get; private set; } = 2;
    public IReadOnlyCollection<PlayMatchPlayer> Players => _players.AsReadOnly();

    internal bool RecordRally(PlayTeam winner, int targetScore, int winBy)
    {
        EnsureActive();
        if (!Enum.IsDefined(winner)) throw new ArgumentException("Choose rally winner A or B.", nameof(winner));
        if (winner != ServingTeam)
        {
            if (CurrentServerNumber == 1) CurrentServerNumber = 2;
            else
            {
                ServingTeam = ServingTeam == PlayTeam.A ? PlayTeam.B : PlayTeam.A;
                CurrentServerNumber = 1;
            }
            return false;
        }
        var score = winner == PlayTeam.A ? TeamAScore : TeamBScore;
        if (score == int.MaxValue) throw new PlayConflictException("The score cannot be increased further. Correct the score.");
        score++;
        if (winner == PlayTeam.A) TeamAScore = score;
        else TeamBScore = score;
        var opponent = winner == PlayTeam.A ? TeamBScore : TeamAScore;
        return score >= targetScore && score - opponent >= winBy;
    }

    internal void CorrectScore(int teamAScore, int teamBScore, PlayTeam servingTeam, int currentServerNumber)
    {
        EnsureActive();
        if (teamAScore < 0) throw new ArgumentException("Scores cannot be negative.", nameof(teamAScore));
        if (teamBScore < 0) throw new ArgumentException("Scores cannot be negative.", nameof(teamBScore));
        if (!Enum.IsDefined(servingTeam)) throw new ArgumentException("Choose serving team A or B.", nameof(servingTeam));
        if (currentServerNumber is not (1 or 2)) throw new ArgumentException("Choose server 1 or 2.", nameof(currentServerNumber));
        TeamAScore = teamAScore;
        TeamBScore = teamBScore;
        ServingTeam = servingTeam;
        CurrentServerNumber = currentServerNumber;
        // Correction repairs state only; it never completes or rotates a game.
    }

    private void EnsureActive()
    {
        if (Status != PlayMatchStatus.Active) throw new PlayConflictException("This game is no longer active.");
    }

    internal void Complete(DateTimeOffset now)
    {
        if (Status != PlayMatchStatus.Active) throw new PlayConflictException("This game is no longer active.");
        Status = PlayMatchStatus.Completed;
        CompletedAt = now.ToUniversalTime();
    }
}

public sealed class PlayMatchPlayer
{
    private PlayMatchPlayer() { }
    internal PlayMatchPlayer(Guid matchId, Guid playerId, int position)
    {
        MatchId = matchId;
        PlayerId = playerId;
        Position = position;
        Team = position <= 2 ? PlayTeam.A : PlayTeam.B;
    }

    public Guid MatchId { get; private set; }
    public Guid PlayerId { get; private set; }
    public int Position { get; private set; }
    public PlayTeam Team { get; private set; }
}
