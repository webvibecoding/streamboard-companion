using Streamboard.Companion.Core.Detection;

namespace Streamboard.Companion.Core.Tracking;

public enum GameTrackingActionKind
{
    Changed,
    Resync,
}

public sealed record GameTrackingAction(GameTrackingActionKind Kind, ResolvedGame Game);

public sealed class GameDetectionStateMachine
{
    private readonly TimeSpan _debounce;
    private readonly TimeSpan _resync;
    private bool _candidateInitialized;
    private string? _candidateKey;
    private ResolvedGame? _candidate;
    private DateTimeOffset _candidateSince;
    private string? _activeKey;
    private DateTimeOffset? _lastPublishedAt;

    public GameDetectionStateMachine(TimeSpan debounce, TimeSpan resync)
    {
        if (debounce < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(debounce));
        }

        if (resync <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(resync));
        }

        _debounce = debounce;
        _resync = resync;
    }

    public GameTrackingAction? Observe(ResolvedGame? game, DateTimeOffset observedAt)
    {
        var key = game?.Key;
        if (!_candidateInitialized || !string.Equals(key, _candidateKey, StringComparison.Ordinal))
        {
            _candidateInitialized = true;
            _candidateKey = key;
            _candidate = game;
            _candidateSince = observedAt;
            return null;
        }

        if (observedAt - _candidateSince < _debounce)
        {
            return null;
        }

        if (!string.Equals(_activeKey, _candidateKey, StringComparison.Ordinal))
        {
            _activeKey = _candidateKey;
            _lastPublishedAt = null;
            if (_candidate is null)
            {
                return null;
            }

            _lastPublishedAt = observedAt;
            return new GameTrackingAction(GameTrackingActionKind.Changed, _candidate);
        }

        if (_candidate is not null
            && (!_lastPublishedAt.HasValue || observedAt - _lastPublishedAt.Value >= _resync))
        {
            _lastPublishedAt = observedAt;
            return new GameTrackingAction(GameTrackingActionKind.Resync, _candidate);
        }

        return null;
    }

    public void MarkDeliveryFailed(DateTimeOffset failedAt, TimeSpan retryAfter)
    {
        if (_activeKey is null)
        {
            return;
        }

        var boundedRetry = retryAfter < TimeSpan.Zero ? TimeSpan.Zero : retryAfter;
        _lastPublishedAt = failedAt - _resync + boundedRetry;
    }

    public void Reset()
    {
        _candidateInitialized = false;
        _candidateKey = null;
        _candidate = null;
        _activeKey = null;
        _lastPublishedAt = null;
    }
}

