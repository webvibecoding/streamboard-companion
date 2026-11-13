using System.Text.Json;
using Streamboard.Companion.Core.Detection;

namespace Streamboard.Companion.Windows;

internal sealed class JsonExecutableMappingStore : IExecutableMappingStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonExecutableMappingStore(string filePath)
    {
        _filePath = filePath;
    }

    public async ValueTask<ExecutableGameMapping?> FindAsync(
        string executableKey,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var mappings = await ReadAllAsync(cancellationToken).ConfigureAwait(false);
            return mappings.GetValueOrDefault(executableKey);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask SetAsync(
        ExecutableGameMapping mapping,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapping.ExecutableKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapping.DisplayName);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var mappings = await ReadAllAsync(cancellationToken).ConfigureAwait(false);
            mappings[mapping.ExecutableKey] = mapping;
            await WriteAllAsync(mappings, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask RemoveAsync(
        string executableKey,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var mappings = await ReadAllAsync(cancellationToken).ConfigureAwait(false);
            if (mappings.Remove(executableKey))
            {
                await WriteAllAsync(mappings, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Dictionary<string, ExecutableGameMapping>> ReadAllAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var mappings = await JsonSerializer.DeserializeAsync<
                Dictionary<string, ExecutableGameMapping>>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
            return mappings is null
                ? new(StringComparer.Ordinal)
                : new(mappings, StringComparer.Ordinal);
        }
        catch (Exception exception) when (
            exception is FileNotFoundException
                or DirectoryNotFoundException
                or JsonException
                or IOException
                or UnauthorizedAccessException)
        {
            return new(StringComparer.Ordinal);
        }
    }

    private async Task WriteAllAsync(
        Dictionary<string, ExecutableGameMapping> mappings,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("Mapping directory is unavailable.");
        Directory.CreateDirectory(directory);
        var temporaryPath = _filePath + ".tmp";
        await using (var stream = new FileStream(
            temporaryPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                mappings,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporaryPath, _filePath, overwrite: true);
    }
}

