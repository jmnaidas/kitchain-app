using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Kitchain.Domain.Play;

namespace Kitchain.Application.Play;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreatePlaySession
{
    [Required] public string Name { get; init; } = "";
    [Required] public DateOnly? Date { get; init; }
    [Required] public TimeOnly? StartTime { get; init; }
    [Required] public TimeOnly? EndTime { get; init; }
    [Required] public int? NumberOfCourts { get; init; }
    public int? MaximumPlayers { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AddPlayGuest([Required] string DisplayName);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecordPlayRally
{
    [Required] public PlayTeam? Winner { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CorrectPlayScore
{
    [Required, Range(0, int.MaxValue)] public int? TeamAScore { get; init; }
    [Required, Range(0, int.MaxValue)] public int? TeamBScore { get; init; }
    [Required] public PlayTeam? ServingTeam { get; init; }
    [Required, Range(1, 2)] public int? CurrentServerNumber { get; init; }
}

public sealed record PlayPlayerDetail(Guid Id, Guid SessionId, string DisplayName, PlayPlayerIdentityType IdentityType,
    PlayPlayerState State, DateTimeOffset JoinedAt, DateTimeOffset UpdatedAt, long? QueueOrder);

public sealed record PlayMatchPlayerDetail(Guid PlayerId, string DisplayName, PlayTeam Team, int Position);
public sealed record PlayMatchDetail(Guid Id, int CourtNumber, PlayMatchStatus Status, DateTimeOffset StartedAt,
    IReadOnlyList<PlayMatchPlayerDetail> Players, int TeamAScore, int TeamBScore, PlayTeam ServingTeam, int CurrentServerNumber);

public sealed record PlaySessionDetail(Guid Id, string JoinCode, string Name, DateOnly SessionDate,
    TimeOnly StartTime, TimeOnly EndTime, int NumberOfCourts, int? MaximumPlayers, PlaySessionStatus Status,
    PlayRotationMode RotationMode, PlayScoringMode ScoringMode, int GameTo, int WinBy,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<PlayPlayerDetail> Players,
    IReadOnlyList<PlayPlayerDetail> WaitingQueue, IReadOnlyList<PlayMatchDetail> ActiveMatches);

public interface IPlayJoinCodeGenerator
{
    string Generate();
}

public interface IPlaySessionStore
{
    // False means only that the generated join code collided with an existing session.
    Task<bool> TryAddAsync(PlaySession session, CancellationToken cancellationToken);
    Task<PlaySession?> FindAsync(string code, CancellationToken cancellationToken);
    Task<PlaySession?> UpdateAsync(string code, Action<PlaySession> update, CancellationToken cancellationToken);
}

public sealed class PlaySessionService(IPlaySessionStore store, IPlayJoinCodeGenerator codes)
{
    public async Task<PlaySessionDetail> CreateAsync(CreatePlaySession input, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var session = new PlaySession(Guid.NewGuid(), codes.Generate(), input.Name,
                input.Date ?? throw new ArgumentException("A session date is required.", "date"),
                input.StartTime ?? throw new ArgumentException("Start time is required.", "startTime"),
                input.EndTime ?? throw new ArgumentException("End time is required.", "endTime"),
                input.NumberOfCourts ?? throw new ArgumentException("Number of courts is required.", "numberOfCourts"),
                input.MaximumPlayers, DateTimeOffset.UtcNow);
            if (await store.TryAddAsync(session, cancellationToken)) return Detail(session);
        }
        throw new PlayConflictException("A join code could not be allocated. Please try creating the session again.");
    }

    public async Task<PlaySessionDetail?> FindAsync(string code, CancellationToken cancellationToken)
    {
        var session = await store.FindAsync(PlaySession.NormalizeCode(code), cancellationToken);
        return session is null ? null : Detail(session);
    }

    public async Task<PlayPlayerDetail?> AddGuestAsync(string code, AddPlayGuest input, CancellationToken cancellationToken)
    {
        PlaySessionPlayer? player = null;
        var session = await store.UpdateAsync(PlaySession.NormalizeCode(code),
            s => player = s.AddGuest(input.DisplayName, Now(s)), cancellationToken);
        return session is null ? null : Player(player!);
    }

    public Task<PlaySessionDetail?> RestAsync(string code, Guid playerId, CancellationToken cancellationToken) =>
        UpdateAsync(code, s => s.Rest(playerId, Now(s)), cancellationToken);

    public Task<PlaySessionDetail?> RejoinAsync(string code, Guid playerId, CancellationToken cancellationToken) =>
        UpdateAsync(code, s => s.Rejoin(playerId, Now(s)), cancellationToken);

    public Task<PlaySessionDetail?> StartAsync(string code, CancellationToken cancellationToken) =>
        UpdateAsync(code, s => s.Start(Now(s)), cancellationToken);

    public Task<PlaySessionDetail?> FinishGameAsync(string code, Guid matchId, CancellationToken cancellationToken) =>
        UpdateAsync(code, s => s.FinishGame(matchId, Now(s)), cancellationToken);

    public Task<PlaySessionDetail?> RecordRallyAsync(string code, Guid matchId, RecordPlayRally input, CancellationToken cancellationToken) =>
        UpdateAsync(code, s => s.RecordRally(matchId,
            input.Winner ?? throw new ArgumentException("Choose a rally winner.", "winner"), Now(s)), cancellationToken);

    public Task<PlaySessionDetail?> CorrectScoreAsync(string code, Guid matchId, CorrectPlayScore input, CancellationToken cancellationToken) =>
        UpdateAsync(code, s => s.CorrectScore(matchId,
            input.TeamAScore ?? throw new ArgumentException("Team A score is required.", "teamAScore"),
            input.TeamBScore ?? throw new ArgumentException("Team B score is required.", "teamBScore"),
            input.ServingTeam ?? throw new ArgumentException("Serving team is required.", "servingTeam"),
            input.CurrentServerNumber ?? throw new ArgumentException("Server number is required.", "currentServerNumber"), Now(s)), cancellationToken);

    private async Task<PlaySessionDetail?> UpdateAsync(string code, Action<PlaySession> update, CancellationToken cancellationToken)
    {
        var session = await store.UpdateAsync(PlaySession.NormalizeCode(code), update, cancellationToken);
        return session is null ? null : Detail(session);
    }

    private static DateTimeOffset Now(PlaySession session)
    {
        var now = DateTimeOffset.UtcNow;
        return now < session.UpdatedAt ? session.UpdatedAt : now;
    }

    private static PlayPlayerDetail Player(PlaySessionPlayer player) => new(player.Id, player.SessionId,
        player.DisplayName, player.IdentityType, player.State, player.JoinedAt, player.UpdatedAt, player.QueueOrder);

    private static PlaySessionDetail Detail(PlaySession session) => new(session.Id, session.JoinCode, session.Name,
        session.SessionDate, session.StartTime, session.EndTime, session.NumberOfCourts, session.MaximumPlayers,
        session.Status, session.RotationMode, session.ScoringMode, session.GameTo, session.WinBy,
        session.CreatedAt, session.UpdatedAt,
        session.Players.OrderBy(p => p.JoinedAt).ThenBy(p => p.Id).Select(Player).ToArray(),
        session.WaitingQueue.Select(Player).ToArray(),
        session.Matches.Where(m => m.Status == PlayMatchStatus.Active).OrderBy(m => m.CourtNumber)
            .Select(m => new PlayMatchDetail(m.Id, m.CourtNumber, m.Status, m.StartedAt,
                m.Players.OrderBy(p => p.Position).Select(p => new PlayMatchPlayerDetail(p.PlayerId,
                    session.Players.Single(player => player.Id == p.PlayerId).DisplayName, p.Team, p.Position)).ToArray(),
                m.TeamAScore, m.TeamBScore, m.ServingTeam, m.CurrentServerNumber))
            .ToArray());
}
