using Kitchain.Domain.Play;

namespace Kitchain.Application.Play;

public sealed record PlayCallOutCount(PlayRallyCallOut CallOut, int Count);

// Read model only. Scores are unavailable in Queue Only, rather than invented 0–0 results.
public sealed record PlayMatchSummary(Guid Id, int CourtNumber, IReadOnlyList<PlayMatchPlayerDetail> Players,
    DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, int? TeamAScore, int? TeamBScore,
    PlayTeam? Winner, int? TotalRallies, int? TaggedRallies, IReadOnlyList<PlayCallOutCount> CallOutCounts,
    IReadOnlyList<PlayRallyEventDetail> Rallies);
