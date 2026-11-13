using Streamboard.Companion.Core.Api;
using Streamboard.Companion.Core.Detection;
using Streamboard.Companion.Core.Tracking;

namespace Streamboard.Companion.Tests;

public sealed class CoordinatorTests
{
    [Fact]
    public async Task OfflineResultIsRetriedAfterThirtySeconds()
    {
        var time = new MutableTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var publisher = new RecordingPublisher(new PlatformSyncResult("twitch", "offline"));
        var coordinator = CreateCoordinator(time, publisher, new TrackingController());

        await coordinator.TickAsync();
        time.Advance(TimeSpan.FromSeconds(10));
        await coordinator.TickAsync();
        time.Advance(TimeSpan.FromSeconds(29));
        await coordinator.TickAsync();
        time.Advance(TimeSpan.FromSeconds(1));
        await coordinator.TickAsync();

        Assert.Equal(2, publisher.Count);
    }

    [Fact]
    public async Task SystemResumeResetsCandidateAndRequiresFreshDebounce()
    {
        var time = new MutableTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var publisher = new RecordingPublisher(new PlatformSyncResult("twitch", "unchanged"));
        var control = new TrackingController();
        var coordinator = CreateCoordinator(time, publisher, control);

        await coordinator.TickAsync();
        time.Advance(TimeSpan.FromSeconds(10));
        await coordinator.TickAsync();
        Assert.Equal(1, publisher.Count);

        control.NotifySystemResume();
        time.Advance(TimeSpan.FromSeconds(1));
        await coordinator.TickAsync();
        Assert.Equal(1, publisher.Count);
        time.Advance(TimeSpan.FromSeconds(10));
        await coordinator.TickAsync();

        Assert.Equal(2, publisher.Count);
    }

    private static GameTrackingCoordinator CreateCoordinator(
        TimeProvider time,
        RecordingPublisher publisher,
        TrackingController control)
    {
        var game = new ResolvedGame(
            "steam:730",
            "steam",
            "Counter-Strike 2",
            "cs2.exe",
            GameConfidence.High);
        return new GameTrackingCoordinator(
            new StaticForeground(time),
            new StaticResolver(game),
            publisher,
            new NullCompanionStatusSink(),
            control,
            GameTrackingOptions.Default,
            time);
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class StaticForeground(TimeProvider time) : IForegroundProcessProvider
    {
        public ValueTask<ForegroundProcessSnapshot?> GetForegroundProcessAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ForegroundProcessSnapshot?>(new(
                123,
                "cs2.exe",
                @"C:\Steam\steamapps\common\CS2\cs2.exe",
                "Counter-Strike 2",
                time.GetUtcNow()));
    }

    private sealed class StaticResolver(ResolvedGame game) : IGameResolver
    {
        public ValueTask<ResolvedGame?> ResolveAsync(
            ForegroundProcessSnapshot process,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ResolvedGame?>(game);
    }

    private sealed class RecordingPublisher(PlatformSyncResult result) : IGameEventPublisher
    {
        public int Count { get; private set; }

        public Task<SyncResponse> PublishAsync(
            ResolvedGame game,
            DateTimeOffset observedAt,
            CancellationToken cancellationToken = default)
        {
            Count++;
            return Task.FromResult(new SyncResponse(Guid.NewGuid().ToString("N"), [result]));
        }
    }
}
