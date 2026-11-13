namespace Streamboard.Companion.Core.Detection;

public static class InstalledGameCatalogLoader
{
    public static IReadOnlyList<GameCatalogEntry> Load(
        IEnumerable<string> steamRoots,
        string? epicManifestsDirectory)
    {
        var entries = new List<GameCatalogEntry>();
        entries.AddRange(LoadSteam(steamRoots));
        entries.AddRange(LoadEpic(epicManifestsDirectory));
        return entries
            .GroupBy(static entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
    }

    public static IReadOnlyList<GameCatalogEntry> LoadSteam(IEnumerable<string> steamRoots)
    {
        var libraryRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in steamRoots.Where(static root => !string.IsNullOrWhiteSpace(root)))
        {
            TryAddFullPath(libraryRoots, root);
            var libraryFile = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            try
            {
                if (!File.Exists(libraryFile))
                {
                    continue;
                }

                foreach (var library in SteamManifestParser.ParseLibraryPaths(File.ReadAllText(libraryFile)))
                {
                    TryAddFullPath(libraryRoots, library);
                }
            }
            catch (Exception) when (File.Exists(libraryFile))
            {
                // One unreadable library must not stop detection in other libraries.
            }
        }

        var entries = new List<GameCatalogEntry>();
        foreach (var libraryRoot in libraryRoots)
        {
            var steamApps = Path.Combine(libraryRoot, "steamapps");
            IEnumerable<string> manifests;
            try
            {
                manifests = Directory.EnumerateFiles(steamApps, "appmanifest_*.acf").ToArray();
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var manifestPath in manifests)
            {
                try
                {
                    var manifest = SteamManifestParser.ParseAppManifest(File.ReadAllText(manifestPath));
                    if (manifest is null)
                    {
                        continue;
                    }

                    entries.Add(new GameCatalogEntry(
                        GameKeyFactory.ForSteam(manifest.AppId),
                        "steam",
                        manifest.Name,
                        Path.Combine(steamApps, "common", manifest.InstallDirectoryName)));
                }
                catch (Exception)
                {
                    // Malformed and concurrently-updated manifests are ignored.
                }
            }
        }

        return entries;
    }

    public static IReadOnlyList<GameCatalogEntry> LoadEpic(string? manifestsDirectory)
    {
        if (string.IsNullOrWhiteSpace(manifestsDirectory))
        {
            return [];
        }

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(manifestsDirectory, "*.item").ToArray();
        }
        catch (Exception)
        {
            return [];
        }

        var entries = new List<GameCatalogEntry>();
        foreach (var file in files)
        {
            try
            {
                var manifest = EpicManifestParser.Parse(File.ReadAllText(file));
                if (manifest is null)
                {
                    continue;
                }

                string? launchPath = null;
                if (!string.IsNullOrWhiteSpace(manifest.LaunchExecutable))
                {
                    launchPath = Path.IsPathRooted(manifest.LaunchExecutable)
                        ? manifest.LaunchExecutable
                        : Path.Combine(manifest.InstallLocation, manifest.LaunchExecutable);
                }

                entries.Add(new GameCatalogEntry(
                    GameKeyFactory.ForEpic(manifest.CatalogNamespace, manifest.ItemId),
                    "epic",
                    manifest.DisplayName,
                    manifest.InstallLocation,
                    launchPath));
            }
            catch (Exception)
            {
                // Ignore an individual broken manifest.
            }
        }

        return entries;
    }

    private static void TryAddFullPath(HashSet<string> paths, string path)
    {
        try
        {
            paths.Add(Path.GetFullPath(path));
        }
        catch (Exception)
        {
            // Invalid library paths are not candidates.
        }
    }
}

