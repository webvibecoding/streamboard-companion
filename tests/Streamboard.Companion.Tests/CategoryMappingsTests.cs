using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Streamboard.Companion.Core.Api;
using Streamboard.Companion.Core.Auth;

namespace Streamboard.Companion.Tests;

public sealed class CategoryMappingsTests
{
    [Fact]
    public async Task LoadGroupsByGameKeyAndSortsWithoutMergingIdenticalNames()
    {
        using var transport = new MappingServer();
        transport.Items.AddRange([
            Mapping("steam:2", "Zenith", "twitch", "200"),
            Mapping("steam:1", "Adventure", "vkplaylive", "vk-10", "exact"),
            Mapping("epic:store:adventure", "Adventure", "vkplaylive", "vk-11"),
            Mapping("steam:1", "Adventure", "twitch", "100"),
        ]);
        using var client = CreateHttpClient(transport);

        var rows = await CreateMappings(client).LoadAsync();

        Assert.Equal(["epic:store:adventure", "steam:1", "steam:2"], rows.Select(row => row.Game.Key));
        Assert.Equal("Не выбрана", rows[0].TwitchLabel);
        Assert.Equal("100", rows[1].Twitch?.Category.Id);
        Assert.Equal("exact", rows[1].VkPlayLive?.ResolutionSource);
        Assert.Equal("Не выбрана", rows[2].VkPlayLiveLabel);
    }

    [Theory]
    [InlineData("twitch", "vkplaylive")]
    [InlineData("vkplaylive", "twitch")]
    public async Task SaveAddsAndReplacesOnlySelectedPlatform(string selected, string other)
    {
        using var transport = new MappingServer();
        var original = Mapping("steam:1", "Adventure", other, "original", "exact");
        transport.Items.Add(original);
        using var client = CreateHttpClient(transport);
        var mappings = CreateMappings(client);
        var row = Assert.Single(await mappings.LoadAsync());

        var categories = await mappings.SearchAsync(selected, "Adventure");
        var found = Assert.Single(categories.Items);
        await mappings.SaveAsync(row.Game, selected, found);
        var replacement = await mappings.SaveAsync(row.Game, selected, new CategorySearchItem("alternate", "Another game", null));

        Assert.Equal("game", replacement.Category.Type);
        Assert.Equal("manual", replacement.ResolutionSource);
        var reopened = Assert.Single(await CreateMappings(client).LoadAsync());
        Assert.Equal(replacement, selected == "twitch" ? reopened.Twitch : reopened.VkPlayLive);
        Assert.Equal(original, other == "twitch" ? reopened.Twitch : reopened.VkPlayLive);
        Assert.All(transport.Writes, saved => Assert.Equal(selected, saved.PlatformId));
    }

    [Fact]
    public async Task FailedSaveLeavesPreviouslySavedCategoriesUntouched()
    {
        using var transport = new MappingServer();
        var original = Mapping("steam:1", "Adventure", "twitch", "original");
        transport.Items.Add(original);
        transport.RejectWrites = true;
        using var client = CreateHttpClient(transport);
        var mappings = CreateMappings(client);

        var error = await Assert.ThrowsAsync<CompanionApiException>(() =>
            mappings.SaveAsync(original.Game, "twitch", new CategorySearchItem("new", "Replacement", "game")));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        Assert.Equal(original, Assert.Single(await mappings.LoadAsync()).Twitch);
    }

    [Fact]
    public async Task UnpairedDeviceReportsMissingConnectionWithoutStartingPairing()
    {
        using var transport = new MappingServer();
        using var client = CreateHttpClient(transport);

        var error = await Assert.ThrowsAsync<CompanionApiException>(
            () => CreateMappings(client, paired: false).LoadAsync());

        Assert.Equal("desktop_pairing_required", error.Reason);
        Assert.True(CategoryMappings.IsDeviceUnauthorized(error));
        Assert.Equal(0, transport.RequestCount);
    }

    private static CategoryMappings CreateMappings(HttpClient client, bool paired = true)
    {
        var api = new StreamboardApiClient(client);
        var credential = paired
            ? new StoredDeviceCredential("access-token", "refresh-token", DateTimeOffset.UtcNow.AddHours(1), "device")
            : null;
        return new CategoryMappings(api, new DeviceTokenManager(
            api, new StoredCredential(credential), new UnexpectedPairing(), "Test PC"));
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler transport) =>
        new(transport, disposeHandler: false) { BaseAddress = new Uri("https://streamboard.sip1ay.pro/") };

    private static DesktopMapping Mapping(
        string key, string name, string platform, string categoryId, string origin = "manual") =>
        new(new SyncGame(key, key.Split(':')[0], name, null), platform,
            new CategorySearchItem(categoryId, name, "game"), origin);

    private sealed class MappingServer : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        public List<DesktopMapping> Items { get; } = [];
        public List<DesktopMapping> Writes { get; } = [];
        public bool RejectWrites { get; set; }
        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            Assert.Equal("access-token", request.Headers.Authorization?.Parameter);
            var uri = request.RequestUri!;
            if (uri.AbsolutePath.EndsWith("/categories", StringComparison.Ordinal))
            {
                Assert.Contains("query=Adventure", uri.Query);
                return Json(new CategorySearchResponse([new CategorySearchItem("found", "Adventure", null)]));
            }

            Assert.Equal("/api/desktop/v1/mappings", uri.AbsolutePath);
            if (request.Method == HttpMethod.Get)
            {
                return Json(new MappingListResponse(Items));
            }

            Assert.Equal(HttpMethod.Put, request.Method);
            if (RejectWrites)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = JsonContent.Create(new { detail = new { code = "desktop_service_unavailable" } }),
                };
            }

            var written = await request.Content!.ReadFromJsonAsync<DesktopMapping>(JsonOptions, cancellationToken);
            var mapping = Assert.IsType<DesktopMapping>(written) with { ResolutionSource = "manual" };
            Writes.Add(mapping);
            Items.RemoveAll(item => item.Game.Key == mapping.Game.Key && item.PlatformId == mapping.PlatformId);
            Items.Add(mapping);
            return Json(mapping);
        }

        private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(value, options: JsonOptions),
        };
    }

    private sealed class StoredCredential(StoredDeviceCredential? value) : ICredentialStore
    {
        public ValueTask<StoredDeviceCredential?> ReadAsync(CancellationToken cancellationToken) => ValueTask.FromResult(value);
        public ValueTask WriteAsync(StoredDeviceCredential credential, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public ValueTask DeleteAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class UnexpectedPairing : IPairingInteraction
    {
        public ValueTask PairingStartedAsync(PairingResponse pairing, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
