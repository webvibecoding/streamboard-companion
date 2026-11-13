namespace Streamboard.Companion.Core.Detection;

public sealed class GameCatalog
{
    private readonly IReadOnlyList<GameCatalogEntry> _entries;

    public GameCatalog(IEnumerable<GameCatalogEntry> entries)
    {
        _entries = entries
            .Where(static entry =>
                !string.IsNullOrWhiteSpace(entry.Key)
                && !string.IsNullOrWhiteSpace(entry.DisplayName)
                && !string.IsNullOrWhiteSpace(entry.InstallDirectory))
            .OrderByDescending(static entry => NormalizeDirectory(entry.InstallDirectory).Length)
            .ThenBy(static entry => entry.Source switch
            {
                "steam" => 0,
                "epic" => 1,
                _ => 2,
            })
            .ToArray();
    }

    public GameCatalogEntry? MatchExecutable(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        string candidate;
        try
        {
            candidate = Path.GetFullPath(executablePath);
        }
        catch (Exception) when (executablePath.Length > 0)
        {
            return null;
        }

        foreach (var entry in _entries)
        {
            if (!string.IsNullOrWhiteSpace(entry.LaunchExecutable)
                && PathsEqual(candidate, entry.LaunchExecutable))
            {
                return entry;
            }

            if (IsUnderDirectory(candidate, entry.InstallDirectory))
            {
                return entry;
            }
        }

        return null;
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool IsUnderDirectory(string filePath, string directory)
    {
        try
        {
            var relative = Path.GetRelativePath(Path.GetFullPath(directory), filePath);
            return relative.Length > 0
                && !Path.IsPathRooted(relative)
                && !relative.Equals("..", StringComparison.Ordinal)
                && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string NormalizeDirectory(string path)
    {
        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception)
        {
            return path;
        }
    }
}

