using System.Net;
using System.Text.Json;
using Streamboard.Companion.Core.Api;
using Streamboard.Companion.Core.Auth;

namespace Streamboard.Companion.Tests;

public sealed class ApiClientTests
{
    [Fact]
    public void PkceChallengeIsUrlSafeAndDeterministic()
    {
        var verifier = Pkce.CreateVerifier();
        var challenge = Pkce.CreateChallenge(verifier);

        Assert.InRange(verifier.Length, 43, 128);
        Assert.Equal(43, challenge.Length);
        Assert.DoesNotContain("+", challenge, StringComparison.Ordinal);
        Assert.DoesNotContain("/", challenge, StringComparison.Ordinal);
        Assert.Equal(challenge, Pkce.CreateChallenge(verifier));
    }

    [Fact]
    public async Task ClientUsesDesktopV1ContractAndBearerAuth()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://streamboard.sip1ay.pro/") };
        var client = new StreamboardApiClient(http);

        await client.StartPairingAsync(new PairingRequest("Gaming PC", new string('A', 43)));
        await client.SyncAsync(
            "access-token",
            new SyncRequest(
                "event-1234",
                DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                new SyncGame("steam:730", "steam", "Counter-Strike 2", "cs2.exe")));

        Assert.Equal("/api/desktop/v1/pairings", handler.Requests[0].Path);
        Assert.Contains("code_challenge", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Equal("/api/desktop/v1/sync", handler.Requests[1].Path);
        Assert.Equal("Bearer", handler.Requests[1].Scheme);
        Assert.Equal("access-token", handler.Requests[1].Token);
        Assert.DoesNotContain("C:\\", handler.Requests[1].Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ManualMappingSendsOnlyWritableFields()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://streamboard.sip1ay.pro/") };
        var client = new StreamboardApiClient(http);
        var mapping = new DesktopMapping(
            new SyncGame("steam:730", "steam", "Counter-Strike 2", "cs2.exe"),
            "twitch",
            new CategorySearchItem("32399", "Counter-Strike 2", null),
            "manual");

        await client.PutMappingAsync("access-token", mapping);

        using var json = JsonDocument.Parse(Assert.Single(handler.Requests).Body);
        var body = json.RootElement;
        Assert.Equal("twitch", body.GetProperty("platform_id").GetString());
        Assert.False(body.TryGetProperty("resolution_source", out _));
        Assert.False(body.GetProperty("category").TryGetProperty("type", out _));
    }

    [Fact]
    public async Task ApiErrorsDoNotReflectTokensFromUntrustedResponseBodies()
    {
        using var http = new HttpClient(new SecretErrorHandler())
        {
            BaseAddress = new Uri("https://streamboard.sip1ay.pro/"),
        };
        var client = new StreamboardApiClient(http);

        var exception = await Assert.ThrowsAsync<CompanionApiException>(() =>
            client.SyncAsync(
                "access-token",
                new SyncRequest(
                    "event-1234",
                    DateTimeOffset.UtcNow,
                    new SyncGame("steam:730", "steam", "Counter-Strike 2", "cs2.exe"))));

        Assert.Equal("desktop_service_unavailable", exception.Reason);
        Assert.DoesNotContain("sbd_rt_sensitive", exception.ToString(), StringComparison.Ordinal);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(
                request.RequestUri!.AbsolutePath,
                body,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter));

            object payload = request.RequestUri.AbsolutePath.EndsWith("/pairings", StringComparison.Ordinal)
                ? new
                {
                    device_code = "device-code-value-123456",
                    user_code = "ABCD-EFGH",
                    verification_uri = "https://streamboard.sip1ay.pro/next/",
                    verification_uri_complete = "https://streamboard.sip1ay.pro/next/?desktop_pairing=ABCD-EFGH",
                    expires_in = 600,
                    interval = 5,
                }
                : new
                {
                    event_id = "event-1234",
                    results = Array.Empty<object>(),
                };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    System.Text.Encoding.UTF8,
                    "application/json"),
            };
        }
    }

    private sealed record CapturedRequest(string Path, string Body, string? Scheme, string? Token);

    private sealed class SecretErrorHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("{\"secret\":\"sbd_rt_sensitive\"}"),
            });
    }
}
