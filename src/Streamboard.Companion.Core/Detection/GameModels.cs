namespace Streamboard.Companion.Core.Detection;

public sealed record ForegroundProcessSnapshot(
    int ProcessId,
    string ExecutableName,
    string? ExecutablePath,
    string? WindowTitle,
    DateTimeOffset ObservedAt);

public enum GameConfidence
{
    Low,
    High,
}

public sealed record ResolvedGame(
    string Key,
    string Source,
    string DisplayName,
    string ExecutableName,
    GameConfidence Confidence,
    bool RequiresMapping = false);

public sealed record GameCatalogEntry(
    string Key,
    string Source,
    string DisplayName,
    string InstallDirectory,
    string? LaunchExecutable = null);

public sealed record ExecutableGameMapping(
    string ExecutableKey,
    string DisplayName,
    string Source = "exe");

public interface IForegroundProcessProvider
{
    ValueTask<ForegroundProcessSnapshot?> GetForegroundProcessAsync(
        CancellationToken cancellationToken = default);
}

public interface IExecutableMappingStore
{
    ValueTask<ExecutableGameMapping?> FindAsync(
        string executableKey,
        CancellationToken cancellationToken = default);

    ValueTask SetAsync(
        ExecutableGameMapping mapping,
        CancellationToken cancellationToken = default);

    ValueTask RemoveAsync(
        string executableKey,
        CancellationToken cancellationToken = default);
}

public interface IGameResolver
{
    ValueTask<ResolvedGame?> ResolveAsync(
        ForegroundProcessSnapshot process,
        CancellationToken cancellationToken = default);
}

