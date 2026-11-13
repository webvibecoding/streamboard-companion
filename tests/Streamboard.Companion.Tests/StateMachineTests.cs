using Streamboard.Companion.Core.Detection;
using Streamboard.Companion.Core.Tracking;

namespace Streamboard.Companion.Tests;

public sealed class StateMachineTests
{
    private static readonly ResolvedGame GameA = new(
        "steam:10", "steam", "Game A", "a.exe", GameConfidence.High);
    private static readonly ResolvedGame GameB = new(
        "epic:namespace:item", "epic", "Game B", "b.exe", GameConfidence.High);

    [Fact]
    public void CandidateMustRemainStableForTenSeconds()
    {
        var state = new GameDetectionStateMachine(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        Assert.Null(state.Observe(GameA, start));
        Assert.Null(state.Observe(GameA, start.AddSeconds(9)));
        var action = state.Observe(GameA, start.AddSeconds(10));

        Assert.NotNull(action);
        Assert.Equal(GameTrackingActionKind.Changed, action.Kind);
    }

    [Fact]
    public void AltTabDoesNotPublishAndReturningGameDebouncesAgain()
    {
        var state = new GameDetectionStateMachine(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        state.Observe(GameA, start);
        Assert.NotNull(state.Observe(GameA, start.AddSeconds(10)));

        Assert.Null(state.Observe(null, start.AddSeconds(11)));
        Assert.Null(state.Observe(null, start.AddSeconds(21)));
        Assert.Null(state.Observe(GameA, start.AddSeconds(22)));
        Assert.Null(state.Observe(GameA, start.AddSeconds(31)));
        Assert.Equal(
            GameTrackingActionKind.Changed,
            state.Observe(GameA, start.AddSeconds(32))?.Kind);
    }

    [Fact]
    public void GameSwitchDebouncesAndActiveGameResyncsEveryThirtySeconds()
    {
        var state = new GameDetectionStateMachine(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        state.Observe(GameA, start);
        Assert.Equal(GameTrackingActionKind.Changed, state.Observe(GameA, start.AddSeconds(10))?.Kind);
        Assert.Null(state.Observe(GameA, start.AddSeconds(39)));
        Assert.Equal(GameTrackingActionKind.Resync, state.Observe(GameA, start.AddSeconds(40))?.Kind);

        Assert.Null(state.Observe(GameB, start.AddSeconds(41)));
        Assert.Null(state.Observe(GameB, start.AddSeconds(50)));
        Assert.Equal(GameTrackingActionKind.Changed, state.Observe(GameB, start.AddSeconds(51))?.Kind);
    }

    [Fact]
    public void FailedDeliveryRetriesAtRequestedDelay()
    {
        var state = new GameDetectionStateMachine(TimeSpan.Zero, TimeSpan.FromSeconds(30));
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        state.Observe(GameA, start);
        Assert.NotNull(state.Observe(GameA, start));
        state.MarkDeliveryFailed(start, TimeSpan.FromSeconds(5));

        Assert.Null(state.Observe(GameA, start.AddSeconds(4)));
        Assert.Equal(GameTrackingActionKind.Resync, state.Observe(GameA, start.AddSeconds(5))?.Kind);
    }
}
