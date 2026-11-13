using System.Security.Cryptography;
using System.Text;
using Streamboard.Companion.Core.Detection;

namespace Streamboard.Companion.Tests;

public sealed class DetectionTests
{
    [Fact]
    public void GameKeysUseStablePublicIdentifiersAndDoNotExposeExecutablePath()
    {
        Assert.Equal("steam:730", GameKeyFactory.ForSteam("730"));
        Assert.Equal("epic:namespace:item", GameKeyFactory.ForEpic("Namespace", "Item"));

        const string path = @"C:\Games\Private Folder\game.exe";
        var expectedHash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes("c:/games/private folder/game.exe"))).ToLowerInvariant();
        var key = GameKeyFactory.ForExecutable(path);

        Assert.Equal($"exe:{expectedHash}", key);
        Assert.DoesNotContain("Private Folder", key, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(68, key.Length);
    }

    [Fact]
    public void SteamFixturesParseLibrariesAndManifest()
    {
        const string libraries = "\"libraryfolders\" { \"0\" { \"path\" \"C:\\\\Steam\" } \"1\" { \"path\" \"D:\\\\Games\" } }";
        const string manifest = "\"AppState\" { \"appid\" \"730\" \"name\" \"Counter-Strike 2\" \"installdir\" \"Counter-Strike Global Offensive\" }";

        Assert.Equal([@"C:\Steam", @"D:\Games"], SteamManifestParser.ParseLibraryPaths(libraries));
        Assert.Equal(
            new SteamManifest("730", "Counter-Strike 2", "Counter-Strike Global Offensive"),
            SteamManifestParser.ParseAppManifest(manifest));
    }

    [Fact]
    public void EpicFixtureUsesNamespaceAndCatalogItem()
    {
        const string json = """
            {
              "CatalogNamespace": "fn",
              "CatalogItemId": "fortnite",
              "DisplayName": "Fortnite",
              "InstallLocation": "C:\\Epic\\Fortnite",
              "LaunchExecutable": "FortniteGame\\Binaries\\Win64\\FortniteClient-Win64-Shipping.exe"
            }
            """;

        var manifest = EpicManifestParser.Parse(json);

        Assert.NotNull(manifest);
        Assert.Equal("fn", manifest.CatalogNamespace);
        Assert.Equal("fortnite", manifest.ItemId);
    }

    [Fact]
    public async Task ResolverPrefersCatalogThenManualRuleAndFiltersLaunchers()
    {
        var catalog = new GameCatalog([
            new GameCatalogEntry(
                "steam:730",
                "steam",
                "Counter-Strike 2",
                @"C:\Steam\steamapps\common\CS2"),
        ]);
        var mappings = new MemoryMappingStore();
        var resolver = new GameResolver(catalog, mappings);
        var observedAt = DateTimeOffset.UtcNow;

        var steam = await resolver.ResolveAsync(new ForegroundProcessSnapshot(
            10,
            "cs2.exe",
            @"C:\Steam\steamapps\common\CS2\game\bin\win64\cs2.exe",
            "Counter-Strike 2",
            observedAt));
        Assert.NotNull(steam);
        Assert.Equal("steam:730", steam.Key);
        Assert.False(steam.RequiresMapping);

        var unknownPath = @"C:\Games\Indie\indie.exe";
        var key = GameKeyFactory.ForExecutable(unknownPath);
        await mappings.SetAsync(new ExecutableGameMapping(key, "Indie Game"));
        var manual = await resolver.ResolveAsync(new ForegroundProcessSnapshot(
            11, "indie.exe", unknownPath, "Indie", observedAt));
        Assert.NotNull(manual);
        Assert.Equal("exe", manual.Source);
        Assert.Equal("Indie Game", manual.DisplayName);

        var launcher = await resolver.ResolveAsync(new ForegroundProcessSnapshot(
            12, "steam.exe", @"C:\Steam\steam.exe", "Steam", observedAt));
        Assert.Null(launcher);
    }

    [Fact]
    public async Task InaccessibleUnknownExecutableRequiresManualMapping()
    {
        var resolver = new GameResolver(new GameCatalog([]), new MemoryMappingStore());

        var game = await resolver.ResolveAsync(new ForegroundProcessSnapshot(
            99,
            "mystery.exe",
            null,
            "Mystery",
            DateTimeOffset.UtcNow));

        Assert.NotNull(game);
        Assert.True(game.RequiresMapping);
        Assert.Equal("exe", game.Source);
    }

    private sealed class MemoryMappingStore : IExecutableMappingStore
    {
        private readonly Dictionary<string, ExecutableGameMapping> _items = new(StringComparer.Ordinal);

        public ValueTask<ExecutableGameMapping?> FindAsync(string executableKey, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_items.GetValueOrDefault(executableKey));

        public ValueTask SetAsync(ExecutableGameMapping mapping, CancellationToken cancellationToken = default)
        {
            _items[mapping.ExecutableKey] = mapping;
            return ValueTask.CompletedTask;
        }

        public ValueTask RemoveAsync(string executableKey, CancellationToken cancellationToken = default)
        {
            _items.Remove(executableKey);
            return ValueTask.CompletedTask;
        }
    }
}
