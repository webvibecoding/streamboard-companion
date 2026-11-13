using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Streamboard.Companion.Core.Api;

public sealed class StreamboardApiClient : IStreamboardApiClient
{
    public const string ApiPrefix = "api/desktop/v1/";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

    public StreamboardApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public Task<PairingResponse> StartPairingAsync(
        PairingRequest request,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<PairingResponse>(
            HttpMethod.Post,
            "pairings",
            request,
            accessToken: null,
            cancellationToken);

    public async Task<TokenResponse> RequestTokenAsync(
        TokenRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            "token",
            request,
            accessToken: null,
            cancellationToken).ConfigureAwait(false);

        var payload = await DeserializeAsync<TokenResponse>(response, cancellationToken)
            .ConfigureAwait(false);
        if (response.IsSuccessStatusCode || !string.IsNullOrWhiteSpace(payload.Error))
        {
            return payload;
        }

        throw await CreateExceptionAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public Task<BootstrapResponse> GetBootstrapAsync(
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<BootstrapResponse>(
            HttpMethod.Get,
            "bootstrap",
            body: null,
            accessToken,
            cancellationToken);

    public Task<SyncResponse> SyncAsync(
        string accessToken,
        SyncRequest request,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<SyncResponse>(
            HttpMethod.Post,
            "sync",
            request,
            accessToken,
            cancellationToken);

    public Task<CategorySearchResponse> SearchCategoriesAsync(
        string accessToken,
        string platformId,
        string query,
        CancellationToken cancellationToken = default)
    {
        var route = $"categories?platform_id={Uri.EscapeDataString(platformId)}&query={Uri.EscapeDataString(query)}";
        return SendJsonAsync<CategorySearchResponse>(
            HttpMethod.Get,
            route,
            body: null,
            accessToken,
            cancellationToken);
    }

    public Task<MappingListResponse> GetMappingsAsync(
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<MappingListResponse>(
            HttpMethod.Get,
            "mappings",
            body: null,
            accessToken,
            cancellationToken);

    public Task<DesktopMapping> PutMappingAsync(
        string accessToken,
        DesktopMapping mapping,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<DesktopMapping>(
            HttpMethod.Put,
            "mappings",
            mapping,
            accessToken,
            cancellationToken);

    public async Task DeleteMappingAsync(
        string accessToken,
        string gameKey,
        string platformId,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Delete,
            $"mappings?game_key={Uri.EscapeDataString(gameKey)}&platform_id={Uri.EscapeDataString(platformId)}",
            body: null,
            accessToken,
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteSessionAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Delete,
            "session",
            body: null,
            accessToken,
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> SendJsonAsync<T>(
        HttpMethod method,
        string route,
        object? body,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(method, route, body, accessToken, cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await DeserializeAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string route,
        object? body,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, ApiPrefix + route);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T> DeserializeAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return value ?? throw new JsonException("The response body was empty.");
        }
        catch (JsonException exception)
        {
            throw new CompanionApiException(response.StatusCode, "invalid_server_response")
            {
                Source = exception.Source,
            };
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(response, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<CompanionApiException> CreateExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var reason = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "desktop_session_unauthorized",
            HttpStatusCode.Forbidden => "desktop_session_forbidden",
            HttpStatusCode.TooManyRequests => "desktop_rate_limited",
            _ when (int)response.StatusCode >= 500 => "desktop_service_unavailable",
            _ => "desktop_request_failed",
        };

        try
        {
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (document.RootElement.TryGetProperty("detail", out var detail)
                && detail.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(detail.GetString()))
            {
                reason = detail.GetString()!;
            }
            else if (document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(error.GetString()))
            {
                reason = error.GetString()!;
            }
        }
        catch (JsonException)
        {
            // Status-derived messages avoid reflecting untrusted response bodies.
        }

        return new CompanionApiException(response.StatusCode, reason);
    }
}

