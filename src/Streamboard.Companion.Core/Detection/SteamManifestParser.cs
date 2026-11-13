using System.Text.RegularExpressions;

namespace Streamboard.Companion.Core.Detection;

public sealed record SteamManifest(string AppId, string Name, string InstallDirectoryName);

public static partial class SteamManifestParser
{
    public static SteamManifest? ParseAppManifest(string content)
    {
        var values = ParseFlatValues(content);
        if (!values.TryGetValue("appid", out var appId)
            || !values.TryGetValue("name", out var name)
            || !values.TryGetValue("installdir", out var installDirectory)
            || string.IsNullOrWhiteSpace(appId)
            || string.IsNullOrWhiteSpace(name)
            || string.IsNullOrWhiteSpace(installDirectory))
        {
            return null;
        }

        return new SteamManifest(appId.Trim(), name.Trim(), installDirectory.Trim());
    }

    public static IReadOnlyList<string> ParseLibraryPaths(string content)
    {
        var paths = new List<string>();
        foreach (Match match in KeyValueRegex().Matches(content ?? string.Empty))
        {
            if (!match.Groups["key"].Value.Equals("path", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var path = UnescapeVdf(match.Groups["value"].Value).Trim();
            if (path.Length > 0)
            {
                paths.Add(path);
            }
        }

        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static Dictionary<string, string> ParseFlatValues(string content)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in KeyValueRegex().Matches(content ?? string.Empty))
        {
            result[UnescapeVdf(match.Groups["key"].Value)] =
                UnescapeVdf(match.Groups["value"].Value);
        }

        return result;
    }

    private static string UnescapeVdf(string value) =>
        value.Replace("\\\\", "\\", StringComparison.Ordinal)
            .Replace("\\\"", "\"", StringComparison.Ordinal);

    [GeneratedRegex("\\\"(?<key>(?:\\\\.|[^\\\"])*)\\\"\\s+\\\"(?<value>(?:\\\\.|[^\\\"])*)\\\"", RegexOptions.CultureInvariant)]
    private static partial Regex KeyValueRegex();
}

