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
    public IReadOnlyCollection<PlayMatchPlayer> Players => _players.AsReadOnly();

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
