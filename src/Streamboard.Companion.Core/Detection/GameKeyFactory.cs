using System.Security.Cryptography;
using System.Text;

namespace Streamboard.Companion.Core.Detection;

public static class GameKeyFactory
{
    public static string ForSteam(string appId)
    {
        var normalized = new string((appId ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (normalized.Length == 0)
        {
            throw new ArgumentException("A Steam app id is required.", nameof(appId));
        }

        return $"steam:{normalized}";
    }

    public static string ForEpic(string catalogNamespace, string itemId) =>
        $"epic:{NormalizeEpicPart(catalogNamespace)}:{NormalizeEpicPart(itemId)}";

    public static string ForExecutable(string executableIdentity)
    {
        var rawIdentity = (executableIdentity ?? string.Empty).Trim();
        if (rawIdentity.Length == 0)
        {
            throw new ArgumentException("An executable identity is required.", nameof(executableIdentity));
        }

        var normalized = NormalizeExecutableIdentity(rawIdentity);
        return $"exe:{FullHash(normalized)}";
    }

    private static string NormalizeEpicPart(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            throw new ArgumentException("An identifier is required.", nameof(value));
        }

        return Uri.EscapeDataString(normalized);
    }

    private static string NormalizeExecutableIdentity(string value)
    {
        var looksLikeWindowsPath =
            value.Length >= 3
            && char.IsAsciiLetter(value[0])
            && value[1] == ':'
            && (value[2] == '\\' || value[2] == '/');
        if (looksLikeWindowsPath || value.StartsWith("\\\\", StringComparison.Ordinal))
        {
            return value.Replace('\\', '/').TrimEnd('/').ToLowerInvariant();
        }

        try
        {
            return Path.IsPathRooted(value)
                ? Path.GetFullPath(value)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace('\\', '/')
                    .ToLowerInvariant()
                : Path.GetFileName(value).ToLowerInvariant();
        }
        catch (Exception)
        {
            return Path.GetFileName(value).ToLowerInvariant();
        }
    }

    private static string FullHash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

