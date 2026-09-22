using System.Reflection;
using Kitchain.Domain.Play;

namespace Kitchain.Tests.Play;

public sealed class PlayRotationVerificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 0, 0, 0, TimeSpan.Zero);
    private static PlaySession Session(PlayRotationMode mode, int count = 8)
    {
        var session = new PlaySession(Guid.NewGuid(), "ABCDEF", "Verification", new(2026, 9, 16),
            new(18, 0), new(21, 0), 1, null, Now, rotationMode: mode);
        for (var i = 0; i < count; i++) session.AddGuest($"Player {i}", Now.AddSeconds(i));
        session.Start(session.UpdatedAt);
        session.StartReadyGames();
        return session;
    }
    private static PlayMatch Current(PlaySession session) => session.Matches.Single(m => m.IsCurrent);
    private static Guid[] Ids(IEnumerable<PlaySessionPlayer> players) => players.Select(p => p.Id).ToArray();

    // Test-only access to the established algorithm provides an exact baseline on identical IDs,
    // history and fairness counters, without changing a running session's locked rotation setting.
    internal static PlaySessionPlayer[] Fair(PlaySession session, IReadOnlyList<PlaySessionPlayer> pool, Guid seed, int count = 4) =>
        (PlaySessionPlayer[])Invoke("PlayFairRotation", "Select", pool, session.Matches, seed, count);
    internal static PlaySessionPlayer[] Pair(PlaySession session, IReadOnlyList<PlaySessionPlayer> selected, Guid seed,
        PlayMatch? avoid = null) => (PlaySessionPlayer[])Invoke("PlayTeamPairing", "Recommend", session.Id, selected, session.Matches, seed, avoid);
    private static object Invoke(string type, string method, params object?[] arguments) =>
        typeof(PlaySession).Assembly.GetType($"Kitchain.Domain.Play.{type}", true)!
            .GetMethod(method, BindingFlags.Public | BindingFlags.Static)!.Invoke(null, arguments)!;

    [Theory]
    [InlineData(PlayRotationMode.WinnersStay)]
    [InlineData(PlayRotationMode.ChallengersStay)]
    [InlineData(PlayRotationMode.SplitTeams)]
    public void No_result_matches_exact_fair_selection_and_pairing_from_identical_state(PlayRotationMode mode)
    {
        var session = Session(mode, 12);
        // Build varied history and opportunity counters, then exercise the fallback repeatedly.
        for (var round = 0; round < 5; round++)
        {
            var match = Current(session);
            session.FinishGame(match.Id, session.UpdatedAt.AddMinutes(10), round < 2 ? PlayTeam.B : null);
            var expected = Pair(session, Fair(session, session.EligibleNextPlayers(match.Id), match.Id), match.Id);
            var before = session.Players.Select(p => (p.Id, p.AdjustedGamesStarted, p.MissedOpportunities, p.QueueOrder)).ToArray();
            var actual = session.NextLineup(match.Id);
            if (round >= 2)
            {
                Assert.Null(match.Winner);
                Assert.Equal(Ids(expected), Ids(actual));
            }
            Assert.Equal(before, session.Players.Select(p => (p.Id, p.AdjustedGamesStarted, p.MissedOpportunities, p.QueueOrder)).ToArray());
            session.StartNextGame(match.Id, Ids(actual), false, session.UpdatedAt);
            session.StartReadyGames();
        }
    }

    [Theory]
    [InlineData(PlayRotationMode.WinnersStay, PlayTeam.A)]
    [InlineData(PlayRotationMode.WinnersStay, PlayTeam.B)]
    [InlineData(PlayRotationMode.ChallengersStay, PlayTeam.A)]
    [InlineData(PlayRotationMode.ChallengersStay, PlayTeam.B)]
    public void Eight_player_pair_retention_uses_exact_fair_incoming_selection(PlayRotationMode mode, PlayTeam winner)
    {
        var session = Session(mode);
        for (var round = 0; round < 4; round++)
        {
            var match = Current(session);
            var now = session.UpdatedAt.AddMinutes(10);
            session.FinishGame(match.Id, now, winner);
            var expectedIncoming = Fair(session, session.WaitingQueue, match.Id, 2);
            var retainedTeam = mode == PlayRotationMode.WinnersStay ? winner : winner == PlayTeam.A ? PlayTeam.B : PlayTeam.A;
            var retainedIds = match.Players.Where(p => p.Team == retainedTeam).Select(p => p.PlayerId).ToArray();
            var next = session.NextLineup(match.Id);
            Assert.Equal(retainedIds.Order(), Ids(next.Take(2)).Order());
            Assert.Equal(Ids(expectedIncoming), Ids(next.Skip(2)));
            Assert.Equal(4, Ids(next).Distinct().Count());
            session.StartNextGame(match.Id, Ids(next), false, now);
            session.StartReadyGames();
            Assert.Equal(Ids(next), Current(session).Players.OrderBy(p => p.Position).Select(p => p.PlayerId));
            Assert.All(match.Players.Where(p => !retainedIds.Contains(p.PlayerId)),
                slot => Assert.Contains(session.WaitingQueue, p => p.Id == slot.PlayerId));
        }
    }

    [Fact]
    public void Fair_rotation_with_a_winner_never_inherits_stay_priority()
    {
        var session = Session(PlayRotationMode.FairRotation);
        var match = Current(session);
        session.FinishGame(match.Id, session.UpdatedAt, PlayTeam.A);
        var expected = Pair(session, Fair(session, session.EligibleNextPlayers(match.Id), match.Id), match.Id);
        Assert.Equal(Ids(expected), Ids(session.NextLineup(match.Id)));
        Assert.Equal(Ids(session.WaitingQueue).Order(), Ids(expected).Order());
        var manual = match.Players.OrderByDescending(p => p.Position).Select(p => p.PlayerId).ToArray();
        session.StartNextGame(match.Id, manual, true, session.UpdatedAt);
        session.StartReadyGames();
        Assert.Equal(manual, Current(session).Players.OrderBy(p => p.Position).Select(p => p.PlayerId));
    }

    [Fact]
    public void Split_teams_repeated_reads_and_consecutive_rotations_preserve_selection_pairing_and_history()
    {
        var session = Session(PlayRotationMode.SplitTeams);
        for (var round = 0; round < 8; round++)
        {
            var match = Current(session);
            var now = session.UpdatedAt.AddMinutes(10);
            session.FinishGame(match.Id, now, round % 2 == 0 ? PlayTeam.A : PlayTeam.B);
            var snapshot = match.Players.OrderBy(p => p.Position).Select(p => (p.PlayerId, p.Team, p.DisplayName)).ToArray();
            var next = session.NextLineup(match.Id).ToArray();
            Assert.Single(match.Players, p => p.Team == PlayTeam.A && next.Any(n => n.Id == p.PlayerId));
            Assert.Single(match.Players, p => p.Team == PlayTeam.B && next.Any(n => n.Id == p.PlayerId));
            Assert.Equal(2, next.Count(p => p.State == PlayPlayerState.Waiting));
            var winner = match.Winner!.Value;
            var retainedWinner = Fair(session, session.Players.Where(p => match.Players.Any(slot => slot.PlayerId == p.Id && slot.Team == winner)).ToArray(), match.Id, 1);
            var retainedLoser = Fair(session, session.Players.Where(p => match.Players.Any(slot => slot.PlayerId == p.Id && slot.Team != winner)).ToArray(), match.Id, 1);
            var selected = retainedWinner.Concat(retainedLoser).Concat(Fair(session, session.WaitingQueue, match.Id, 2)).ToArray();
            Assert.Equal(Ids(Pair(session, selected, match.Id, match)), Ids(next));
            for (var read = 0; read < 20; read++) Assert.Equal(Ids(next), Ids(session.NextLineup(match.Id)));
            session.StartNextGame(match.Id, Ids(next), false, now);
            session.StartReadyGames();
            Assert.Equal(snapshot, match.Players.OrderBy(p => p.Position).Select(p => (p.PlayerId, p.Team, p.DisplayName)).ToArray());
            Assert.Equal(4, session.Matches.Where(m => m.IsCurrent).SelectMany(m => m.Players).Select(p => p.PlayerId).Distinct().Count());
        }
    }

    [Theory]
    [InlineData(PlayRotationMode.WinnersStay)]
    [InlineData(PlayRotationMode.ChallengersStay)]
    [InlineData(PlayRotationMode.SplitTeams)]
    public void Retention_policy_falls_back_if_eligibility_provider_excludes_a_previous_player(PlayRotationMode mode)
    {
        var session = Session(mode);
        var match = Current(session);
        session.FinishGame(match.Id, session.UpdatedAt, PlayTeam.A);
        // Current aggregate rules prohibit removing/resting a held player. Exercise the policy's
        // defensive boundary with an excluded candidate, without inventing an allowed lifecycle.
        var excluded = match.Players.First().PlayerId;
        var eligible = session.EligibleNextPlayers(match.Id).Where(p => p.Id != excluded).ToArray();
        var actual = (IReadOnlyList<PlaySessionPlayer>)Invoke("PlayRotationPolicy", "Recommend",
            session.Id, mode, eligible, session.Matches, match.Id);
        Assert.DoesNotContain(actual, p => p.Id == excluded);
        Assert.Equal(Ids(Pair(session, Fair(session, eligible, match.Id), match.Id)), Ids(actual));
        Assert.Equal(4, actual.Select(p => p.Id).Distinct().Count());
    }
}
