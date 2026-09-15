using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Kitchain.Domain.Play;

namespace Kitchain.Application.Play;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreatePlaySession
{
    [Required] public string Name { get; init; } = "";
    [Required] public DateOnly? Date { get; init; }
    public DateOnly? EndDate { get; init; }
    [Required] public TimeOnly? StartTime { get; init; }
    [Required] public TimeOnly? EndTime { get; init; }
    [Required] public int? NumberOfCourts { get; init; }
    public int? MaximumPlayers { get; init; }
    public PlaySessionMode Mode { get; init; } = PlaySessionMode.QueueOnly;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AddPlayGuest([Required] string DisplayName);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StartNextPlayGame
{
    [Required, MinLength(4), MaxLength(4)] public Guid[]? PlayerIds { get; init; }
    public bool OverrideLineup { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FinishPlayGame
{
    public PlayTeam? Winner { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecordPlayRally
{
    // Each accepted POST is one rally. Without an idempotency key, identical HTTP
    // requests cannot be distinguished from legitimate consecutive rally wins.
    [Required] public PlayTeam? Winner { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EditPlayRallyCallOut
{
    [JsonRequired] public PlayRallyCallOut? CallOut { get; init; }
    [JsonRequired] public PlayRallyCallOut? ExpectedCallOut { get; init; }
}

public sealed record PlayRallyEventDetail(Guid Id, Guid MatchId, long Sequence, PlayTeam Winner,
    bool PointAwarded, PlayRallyCallOut? CallOut, int TeamAScore, int TeamBScore,
    PlayTeam ServingTeam, int CurrentServerNumber, DateTimeOffset CreatedAt);

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
    IReadOnlyList<PlayMatchPlayerDetail> Players, int TeamAScore, int TeamBScore, PlayTeam ServingTeam, int CurrentServerNumber,
    DateTimeOffset? CompletedAt, PlayTeam? Winner, IReadOnlyList<PlayMatchPlayerDetail> NextLineup,
    IReadOnlyList<PlayPlayerDetail> EligiblePlayers, IReadOnlyList<PlayRallyEventDetail> Rallies);

public sealed record PlaySessionDetail(Guid Id, string JoinCode, string Name, DateOnly SessionDate,
    TimeOnly StartTime, TimeOnly EndTime, int NumberOfCourts, int? MaximumPlayers, PlaySessionStatus Status,
    PlayRotationMode RotationMode, PlayScoringMode ScoringMode, int GameTo, int WinBy,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<PlayPlayerDetail> Players,
    IReadOnlyList<PlayPlayerDetail> WaitingQueue, IReadOnlyList<PlayMatchDetail> ActiveMatches,
    PlaySessionMode Mode, IReadOnlyList<PlayMatchDetail> CurrentMatches, DateOnly EndDate,
    IReadOnlyList<PlayMatchSummary> MatchHistory, PlayInsights Insights);

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
                input.MaximumPlayers, DateTimeOffset.UtcNow, input.Mode, input.EndDate);
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

    public Task<PlaySessionDetail?> EditAsync(string code, CreatePlaySession input, CancellationToken cancellationToken) =>
        UpdateAsync(code, s => s.EditDetails(input.Name,
            input.Date ?? throw new ArgumentException("A start date is required.", "date"),
            input.StartTime ?? throw new ArgumentException("A start time is required.", "startTime"),
            input.EndDate ?? input.Date ?? throw new ArgumentException("An end date is required.", "endDate"),
            input.EndTime ?? throw new ArgumentException("An end time is required.", "endTime"),
            input.NumberOfCourts ?? throw new ArgumentException("Number of courts is required.", "numberOfCourts"),
            input.MaximumPlayers, input.Mode, Now(s)), cancellationToken);

    public Task<PlaySessionDetail?> RestAsync(string code, Guid playerId, CancellationToken cancellationToken) =>
        UpdateAsync(code, s => s.Rest(playerId, Now(s)), cancellationToken);

    public Task<PlaySessionDetail?> RenameGuestAsync(string code, Guid playerId, AddPlayGuest input, CancellationToken cancellationToken) =>
        UpdateAsync(code, s => s.RenameGuest(playerId, input.DisplayName, Now(s)), cancellationToken);

    public Task<PlaySessionDetail?> RemoveGuestAsync(string code, Guid playerId, CancellationToken cancellationToken) =>
        UpdateAsync(code, s => s.RemoveGuest(playerId, Now(s)), cancellationToken);

    public Task<PlaySessionDetail?> EndAsync(string code, CancellationToken cancellationToken) =>
        UpdateAsync(code, s => s.End(Now(s)), cancellationToken);

    public Task<PlaySessionDetail?> StartNextGameAsync(string code, Guid matchId, StartNextPlayGame input, CancellationToken cancellationToken) =>
        UpdateAsync(code, s => s.StartNextGame(matchId,
            input.PlayerIds ?? throw new ArgumentException("Choose four eligible players.", "playerIds"), input.OverrideLineup, Now(s)), cancellationToken);

    public Task<PlaySessionDetail?> RejoinAsync(string code, Guid playerId, CancellationToken cancellationToken) =>
        UpdateAsync(code, s => s.Rejoin(playerId, Now(s)), cancellationToken);

    public Task<PlaySessionDetail?> StartAsync(string code, CancellationToken cancellationToken) =>
        UpdateAsync(code, s => s.Start(Now(s)), cancellationToken);

    public Task<PlaySessionDetail?> FinishGameAsync(string code, Guid matchId, CancellationToken cancellationToken, PlayTeam? winner = null) =>
        UpdateAsync(code, s => s.FinishGame(matchId, Now(s), winner), cancellationToken);

    public Task<PlaySessionDetail?> RecordRallyAsync(string code, Guid matchId, RecordPlayRally input, CancellationToken cancellationToken) =>
        UpdateAsync(code, s => s.RecordRally(matchId,
            input.Winner ?? throw new ArgumentException("Choose a rally winner.", "winner"), Now(s)), cancellationToken);

    public Task<PlaySessionDetail?> EditRallyCallOutAsync(string code, Guid matchId, Guid rallyId,
        EditPlayRallyCallOut input, CancellationToken cancellationToken) =>
        UpdateAsync(code, s => s.EditRallyCallOut(matchId, rallyId, input.CallOut, input.ExpectedCallOut, Now(s)), cancellationToken);

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

    private static PlaySessionDetail Detail(PlaySession session)
    {
        PlayMatchPlayerDetail[] Participants(PlayMatch match) => match.Players.OrderBy(p => p.Position)
            .Select(p => new PlayMatchPlayerDetail(p.PlayerId,
                session.Players.Single(player => player.Id == p.PlayerId).DisplayName, p.Team, p.Position)).ToArray();
        PlayRallyEventDetail[] Rallies(PlayMatch match) => match.Rallies.OrderBy(r => r.Sequence)
            .Select(r => new PlayRallyEventDetail(r.Id, r.MatchId, r.Sequence, r.Winner, r.PointAwarded,
                r.CallOut, r.TeamAScore, r.TeamBScore, r.ServingTeam, r.CurrentServerNumber, r.CreatedAt)).ToArray();
        PlayMatchDetail Match(PlayMatch match) => new(match.Id, match.CourtNumber, match.Status, match.StartedAt,
            Participants(match),
            match.TeamAScore, match.TeamBScore, match.ServingTeam, match.CurrentServerNumber, match.CompletedAt, match.Winner,
            match.Status == PlayMatchStatus.Completed && session.Status == PlaySessionStatus.Active
                ? session.NextLineup(match.Id).Select((p, i) => new PlayMatchPlayerDetail(p.Id, p.DisplayName,
                    i < 2 ? PlayTeam.A : PlayTeam.B, i + 1)).ToArray() : [],
            match.Status == PlayMatchStatus.Completed && session.Status == PlaySessionStatus.Active
                ? session.EligibleNextPlayers(match.Id).Select(Player).ToArray() : [],
            Rallies(match));
        var scoring = session.Mode == PlaySessionMode.LiveScoring;
        var history = session.Matches.Where(m => m.Status == PlayMatchStatus.Completed)
            .OrderByDescending(m => m.CompletedAt).ThenByDescending(m => m.StartedAt).ThenByDescending(m => m.Id)
            .Select(m => new PlayMatchSummary(m.Id, m.CourtNumber, Participants(m), m.StartedAt, m.CompletedAt,
                scoring ? m.TeamAScore : null, scoring ? m.TeamBScore : null, m.Winner,
                scoring ? m.Rallies.Count : null, scoring ? m.Rallies.Count(r => r.CallOut.HasValue) : null,
                scoring ? m.Rallies.Where(r => r.CallOut.HasValue).GroupBy(r => r.CallOut!.Value)
                    .OrderBy(g => g.Key).Select(g => new PlayCallOutCount(g.Key, g.Count())).ToArray() : [],
                scoring ? Rallies(m) : [])).ToArray();
        var current = session.Matches.Where(m => m.IsCurrent).OrderBy(m => m.CourtNumber).Select(Match).ToArray();
        return new(session.Id, session.JoinCode, session.Name, session.SessionDate, session.StartTime, session.EndTime,
            session.NumberOfCourts, session.MaximumPlayers, session.Status, session.RotationMode, session.ScoringMode,
            session.GameTo, session.WinBy, session.CreatedAt, session.UpdatedAt,
            session.Players.OrderBy(p => p.JoinedAt).ThenBy(p => p.Id).Select(Player).ToArray(),
            session.WaitingQueue.Select(Player).ToArray(), current.Where(m => m.Status == PlayMatchStatus.Active).ToArray(),
            session.Mode, current, session.EndDate, history, PlayInsights.From(session, history));
    }
}
