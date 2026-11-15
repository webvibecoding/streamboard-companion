using System.Text.Json.Serialization;

namespace Streamboard.Companion.Core.Api;

public sealed record PairingRequest(
    [property: JsonPropertyName("device_name")] string DeviceName,
    [property: JsonPropertyName("code_challenge")] string CodeChallenge);

public sealed record PairingResponse(
    [property: JsonPropertyName("device_code")] string DeviceCode,
    [property: JsonPropertyName("user_code")] string UserCode,
    [property: JsonPropertyName("verification_uri")] string VerificationUri,
    [property: JsonPropertyName("verification_uri_complete")] string? VerificationUriComplete,
    [property: JsonPropertyName("expires_in")] int ExpiresInSeconds,
    [property: JsonPropertyName("interval")] int PollIntervalSeconds);

public sealed record TokenRequest(
    [property: JsonPropertyName("grant_type")] string GrantType,
    [property: JsonPropertyName("device_code")] string? DeviceCode = null,
    [property: JsonPropertyName("code_verifier")] string? CodeVerifier = null,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken = null);

public sealed record TokenResponse(
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("token_type")] string? TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresInSeconds,
    [property: JsonPropertyName("device_id")] string? DeviceId,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("error_description")] string? ErrorDescription);

public sealed record BootstrapResponse(
    [property: JsonPropertyName("device")] BootstrapDevice Device,
    [property: JsonPropertyName("platforms")] IReadOnlyList<BootstrapPlatform> Platforms);

public sealed record BootstrapDevice(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name);

public sealed record BootstrapPlatform(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("connected")] bool Connected,
    [property: JsonPropertyName("live")] bool Live,
    [property: JsonPropertyName("capabilities")] ProviderCapabilities Capabilities);

public sealed record ProviderCapabilities(
    [property: JsonPropertyName("category")] bool Category,
    [property: JsonPropertyName("tags")] bool Tags);

public sealed record SyncGame(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("executable_name")] string? ExecutableName);

public sealed record SyncRequest(
    [property: JsonPropertyName("event_id")] string EventId,
    [property: JsonPropertyName("observed_at")] DateTimeOffset ObservedAt,
    [property: JsonPropertyName("game")] SyncGame Game);

public sealed record SyncResponse(
    [property: JsonPropertyName("event_id")] string? EventId,
    [property: JsonPropertyName("results")] IReadOnlyList<PlatformSyncResult> Results)
{
    public static SyncResponse Empty { get; } = new(null, []);
}

public sealed record PlatformSyncResult(
    [property: JsonPropertyName("platform_id")] string PlatformId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("error")] string? Error = null,
    [property: JsonPropertyName("category")] CategorySearchItem? Category = null,
    [property: JsonPropertyName("candidates")] IReadOnlyList<CategorySearchItem>? Candidates = null);

public sealed record CategorySearchResponse(
    [property: JsonPropertyName("items")] IReadOnlyList<CategorySearchItem> Items);

public sealed record CategorySearchItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("image_url")] string? ImageUrl = null);

public sealed record DesktopMapping(
    [property: JsonPropertyName("game")] SyncGame Game,
    [property: JsonPropertyName("platform_id")] string PlatformId,
    [property: JsonPropertyName("category")] CategorySearchItem Category,
    [property: JsonPropertyName("resolution_source")] string? ResolutionSource = null);

public sealed record MappingListResponse(
    [property: JsonPropertyName("items")] IReadOnlyList<DesktopMapping> Items);

