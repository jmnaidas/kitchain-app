using Kitchain.Application.Play;
using Kitchain.Domain.Play;

namespace Kitchain.Tests.Play;

public sealed class PlaySessionServiceTests
{
    internal static readonly CreatePlaySession Input = new()
    {
        Name = "Evening play", Date = new(2026, 9, 10), StartTime = new(18, 0), EndTime = new(21, 0), NumberOfCourts = 2
    };

    [Theory]
    [InlineData(PlayRotationMode.FairRotation)]
    [InlineData(PlayRotationMode.WinnersStay)]
    [InlineData(PlayRotationMode.ChallengersStay)]
    [InlineData(PlayRotationMode.SplitTeams)]
    public async Task Rotation_setting_is_stored_and_returned(PlayRotationMode mode)
    {
        var store = new CollisionStore(0);
        var result = await new PlaySessionService(store, new SequenceCodes("ABCDEF"))
            .CreateAsync(Input with { RotationMode = mode }, default);
        Assert.Equal(mode, result.RotationMode);
        Assert.Equal(mode, Assert.Single(store.Attempts).RotationMode);
    }

    [Fact]
    public async Task Join_code_collision_retries_with_a_fresh_code_and_keeps_defaults_server_owned()
    {
        var store = new CollisionStore(1);
        var service = new PlaySessionService(store, new SequenceCodes("ABCDEF", "GHJKLM"));
        var result = await service.CreateAsync(Input, default);
        Assert.Equal(2, store.Attempts.Count);
        Assert.NotEqual(store.Attempts[0].Id, result.Id);
        Assert.Equal("GHJKLM", result.JoinCode);
        Assert.Equal(PlaySessionStatus.Draft, result.Status);
        Assert.Equal(PlaySessionMode.QueueOnly, result.Mode);
        Assert.Equal(11, result.GameTo);
        Assert.Equal(PlayRotationMode.FairRotation, result.RotationMode);
        Assert.Empty(result.WaitingQueue);
    }

    [Fact]
    public async Task Persistent_collisions_stop_after_five_attempts()
    {
        var store = new CollisionStore(5);
        var service = new PlaySessionService(store, new SequenceCodes("ABCDEF", "ABCDEF", "ABCDEF", "ABCDEF", "ABCDEF"));
        await Assert.ThrowsAsync<PlayConflictException>(() => service.CreateAsync(Input, default));
        Assert.Equal(5, store.Attempts.Count);
    }

    [Fact]
    public async Task Selected_live_scoring_mode_is_stored_and_returned()
    {
        var store = new CollisionStore(0);
        var result = await new PlaySessionService(store, new SequenceCodes("ABCDEF"))
            .CreateAsync(Input with { Mode = PlaySessionMode.LiveScoring }, default);
        Assert.Equal(PlaySessionMode.LiveScoring, result.Mode);
        Assert.Equal(PlaySessionMode.LiveScoring, Assert.Single(store.Attempts).Mode);
    }

    internal sealed class SequenceCodes(params string[] codes) : IPlayJoinCodeGenerator
    {
        private readonly Queue<string> _codes = new(codes);
        public string Generate() => _codes.Dequeue();
    }

    private sealed class CollisionStore(int collisions) : IPlaySessionStore
    {
        public List<PlaySession> Attempts { get; } = [];
        public Task<bool> TryAddAsync(PlaySession session, CancellationToken cancellationToken)
        {
            Attempts.Add(session);
            return Task.FromResult(Attempts.Count > collisions);
        }
        public Task<PlaySession?> FindAsync(string code, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlaySession?> UpdateAsync(string code, Action<PlaySession> update, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
