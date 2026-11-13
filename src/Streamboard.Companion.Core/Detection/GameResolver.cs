using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Streamboard.Companion.Core.Detection;

public sealed partial class GameResolver : IGameResolver
{
    private readonly GameCatalog _catalog;
    private readonly IExecutableMappingStore _mappings;
    private readonly ExecutableDenylist _denylist;

    public GameResolver(
        GameCatalog catalog,
        IExecutableMappingStore mappings,
        ExecutableDenylist? denylist = null)
    {
        _catalog = catalog;
        _mappings = mappings;
        _denylist = denylist ?? new ExecutableDenylist();
    }

    public async ValueTask<ResolvedGame?> ResolveAsync(
        ForegroundProcessSnapshot process,
        CancellationToken cancellationToken = default)
    {
        var executableName = NormalizeExecutableName(process.ExecutableName, process.ExecutablePath);
        if (executableName.Length == 0 || _denylist.Contains(executableName))
        {
            return null;
        }

        var executableKey = GameKeyFactory.ForExecutable(
            string.IsNullOrWhiteSpace(process.ExecutablePath)
                ? executableName
                : process.ExecutablePath);
        var manual = await _mappings.FindAsync(executableKey, cancellationToken).ConfigureAwait(false);
        if (manual is not null)
        {
            return new ResolvedGame(
                executableKey,
                "exe",
                manual.DisplayName,
                executableName,
                GameConfidence.High);
        }

        if (!string.IsNullOrWhiteSpace(process.ExecutablePath))
        {
            var catalogEntry = _catalog.MatchExecutable(process.ExecutablePath);
            if (catalogEntry is not null)
            {
                return new ResolvedGame(
                    catalogEntry.Key,
                    catalogEntry.Source,
                    catalogEntry.DisplayName,
                    executableName,
                    GameConfidence.High);
            }
        }

        var (inferredName, metadataReliable) = InferDisplayName(
            process.ExecutablePath,
            executableName);
        return new ResolvedGame(
            executableKey,
            "exe",
            inferredName,
            executableName,
            metadataReliable ? GameConfidence.High : GameConfidence.Low,
            RequiresMapping: !metadataReliable);
    }

    private static string NormalizeExecutableName(string name, string? path)
    {
        var fileName = Path.GetFileName(name ?? string.Empty).Trim();
        if (fileName.Length == 0 && !string.IsNullOrWhiteSpace(path))
        {
            fileName = Path.GetFileName(path).Trim();
        }

        if (fileName.Length > 0 && Path.GetExtension(fileName).Length == 0)
        {
            fileName += ".exe";
        }

        return fileName.ToLowerInvariant();
    }

    private static (string DisplayName, bool Reliable) InferDisplayName(
        string? path,
        string executableName)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                var productName = SanitizeProductName(info.ProductName)
                    ?? SanitizeProductName(info.FileDescription);
                if (productName is not null)
                {
                    return (productName, true);
                }
            }
            catch (Exception)
            {
                // Metadata is best effort; the executable basename remains local-only fallback data.
            }
        }

        var stem = Path.GetFileNameWithoutExtension(executableName);
        var spaced = WordSeparatorRegex().Replace(stem, " ").Trim();
        return (spaced.Length == 0 ? "Unknown game" : spaced, false);
    }

    private static string? SanitizeProductName(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.Equals("Microsoft® Windows® Operating System", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Microsoft Windows", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Application", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Game", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Unity Player", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return normalized.Length <= 140 ? normalized : normalized[..140];
    }

    [GeneratedRegex("[_\\-.]+", RegexOptions.CultureInvariant)]
    private static partial Regex WordSeparatorRegex();
}

