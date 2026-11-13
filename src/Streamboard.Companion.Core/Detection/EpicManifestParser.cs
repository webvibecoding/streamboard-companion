using System.Text.Json;

namespace Streamboard.Companion.Core.Detection;

public sealed record EpicManifest(
    string CatalogNamespace,
    string ItemId,
    string DisplayName,
    string InstallLocation,
    string? LaunchExecutable);

public static class EpicManifestParser
{
    public static EpicManifest? Parse(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            var catalogNamespace = ReadString(root, "CatalogNamespace")
                ?? ReadString(root, "Namespace");
            var itemId = ReadString(root, "CatalogItemId")
                ?? ReadString(root, "ItemId");
            var displayName = ReadString(root, "DisplayName");
            var installLocation = ReadString(root, "InstallLocation");
            if (catalogNamespace is null
                || itemId is null
                || displayName is null
                || installLocation is null)
            {
                return null;
            }

            return new EpicManifest(
                catalogNamespace,
                itemId,
                displayName,
                installLocation,
                ReadString(root, "LaunchExecutable"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = property.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

