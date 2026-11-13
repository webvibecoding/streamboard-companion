using Microsoft.Win32;
using Streamboard.Companion.Core.Detection;

namespace Streamboard.Companion.Windows;

internal static class InstalledGameCatalogFactory
{
    public static GameCatalog Create()
    {
        var steamRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddIfDirectory(steamRoots, ReadRegistryString(
            Registry.CurrentUser,
            @"Software\Valve\Steam",
            "SteamPath"));
        AddIfDirectory(steamRoots, ReadRegistryString(
            Registry.LocalMachine,
            @"SOFTWARE\WOW6432Node\Valve\Steam",
            "InstallPath"));

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        AddIfDirectory(steamRoots, Path.Combine(programFilesX86, "Steam"));

        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var epicManifests = Path.Combine(
            programData,
            "Epic",
            "EpicGamesLauncher",
            "Data",
            "Manifests");
        return new GameCatalog(InstalledGameCatalogLoader.Load(steamRoots, epicManifests));
    }

    private static string? ReadRegistryString(
        RegistryKey root,
        string subKey,
        string valueName)
    {
        try
        {
            using var key = root.OpenSubKey(subKey, writable: false);
            return key?.GetValue(valueName) as string;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void AddIfDirectory(HashSet<string> roots, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (Directory.Exists(fullPath))
            {
                roots.Add(fullPath);
            }
        }
        catch (Exception)
        {
            // Invalid registry values are ignored.
        }
    }
}

