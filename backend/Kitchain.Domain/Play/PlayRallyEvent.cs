namespace Kitchain.Domain.Play;

// Descriptive tags only: these values do not affect scoring or rotation.
public enum PlayRallyCallOut { Drive, Dink, Lob, Fault, Out, Kitchen, ServiceBreak }

public sealed class PlayRallyEvent
{
    private PlayRallyEvent() { }

    internal PlayRallyEvent(PlayMatch match, long sequence, PlayTeam winner, bool pointAwarded, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        MatchId = match.Id;
        Sequence = sequence;
        Winner = winner;
        PointAwarded = pointAwarded;
        // Immutable AFTER-rally snapshot. Corrections may create gaps between snapshots;
        // they never rewrite recorded history or manufacture missing rallies.
        TeamAScore = match.TeamAScore;
        TeamBScore = match.TeamBScore;
        ServingTeam = match.ServingTeam;
        CurrentServerNumber = match.CurrentServerNumber;
        CreatedAt = now.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid MatchId { get; private set; }
    public long Sequence { get; private set; }
    public PlayTeam Winner { get; private set; }
    public bool PointAwarded { get; private set; }
    public PlayRallyCallOut? CallOut { get; private set; }
    public int TeamAScore { get; private set; }
    public int TeamBScore { get; private set; }
    public PlayTeam ServingTeam { get; private set; }
    public int CurrentServerNumber { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    internal void EditCallOut(PlayRallyCallOut? callOut, PlayRallyCallOut? expectedCallOut)
    {
        if (callOut.HasValue && !Enum.IsDefined(callOut.Value))
            throw new ArgumentException("Choose a supported call-out.", nameof(callOut));
        if (expectedCallOut.HasValue && !Enum.IsDefined(expectedCallOut.Value))
            throw new ArgumentException("Choose a supported previous call-out.", nameof(expectedCallOut));
        if (CallOut != expectedCallOut)
            throw new PlayConflictException("This rally call-out has changed. Refresh before editing it.");
        CallOut = callOut;
    }
}
