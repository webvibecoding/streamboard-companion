using Streamboard.Companion.Core.Api;
using Streamboard.Companion.Core.Detection;

namespace Streamboard.Companion.Core.Tracking;

public enum CompanionRunState
{
    Starting,
    Searching,
    Debouncing,
    Tracking,
    Paused,
    Pairing,
    Error,
}

public sealed record CompanionStatus(
    CompanionRunState State,
    string Message,
    ResolvedGame? Game = null,
    IReadOnlyList<PlatformSyncResult>? Results = null);

public interface ICompanionStatusSink
{
    ValueTask UpdateStatusAsync(
        CompanionStatus status,
        CancellationToken cancellationToken = default);

    ValueTask MappingRequiredAsync(
        ResolvedGame unresolvedGame,
        CancellationToken cancellationToken = default);
}

public sealed class NullCompanionStatusSink : ICompanionStatusSink
{
    public ValueTask UpdateStatusAsync(
        CompanionStatus status,
        CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public ValueTask MappingRequiredAsync(
        ResolvedGame unresolvedGame,
        CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}

public sealed class TrackingController
{
    private int _paused;
    private int _resumeRevision;

    public bool IsPaused => Volatile.Read(ref _paused) == 1;

    public int ResumeRevision => Volatile.Read(ref _resumeRevision);

    public bool SetPaused(bool paused)
    {
        var previous = Interlocked.Exchange(ref _paused, paused ? 1 : 0);
        return previous != (paused ? 1 : 0);
    }

    public void NotifySystemResume() => Interlocked.Increment(ref _resumeRevision);
}

