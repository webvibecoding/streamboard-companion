using System.Net;
using System.Security.Cryptography;
using Streamboard.Companion.Core.Api;

namespace Streamboard.Companion.Core.Auth;

public sealed record StoredDeviceCredential(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    string? DeviceId);

public interface ICredentialStore
{
    ValueTask<StoredDeviceCredential?> ReadAsync(CancellationToken cancellationToken = default);

    ValueTask WriteAsync(
        StoredDeviceCredential credential,
        CancellationToken cancellationToken = default);

    ValueTask DeleteAsync(CancellationToken cancellationToken = default);
}

public interface IPairingInteraction
{
    ValueTask PairingStartedAsync(
        PairingResponse pairing,
        CancellationToken cancellationToken = default);
}

public static class Pkce
{
    public static string CreateVerifier()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64Url(bytes);
    }

    public static string CreateChallenge(string verifier) =>
        Base64Url(SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier)));

    private static string Base64Url(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}

public sealed class DeviceTokenManager
{
    private static readonly TimeSpan ExpirySkew = TimeSpan.FromMinutes(1);

    private readonly IStreamboardApiClient _api;
    private readonly ICredentialStore _credentials;
    private readonly IPairingInteraction _pairingInteraction;
    private readonly TimeProvider _timeProvider;
    private readonly string _deviceName;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DeviceTokenManager(
        IStreamboardApiClient api,
        ICredentialStore credentials,
        IPairingInteraction pairingInteraction,
        string deviceName,
        TimeProvider? timeProvider = null)
    {
        _api = api;
        _credentials = credentials;
        _pairingInteraction = pairingInteraction;
        _deviceName = string.IsNullOrWhiteSpace(deviceName) ? "Windows PC" : deviceName.Trim();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<string> GetAccessTokenAsync(
        CancellationToken cancellationToken = default,
        bool allowPairing = false)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var stored = await _credentials.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (stored is not null && stored.ExpiresAt - ExpirySkew > _timeProvider.GetUtcNow())
            {
                return stored.AccessToken;
            }

            if (stored is not null && !string.IsNullOrWhiteSpace(stored.RefreshToken))
            {
                var refreshed = await TryRefreshAsync(stored, cancellationToken).ConfigureAwait(false);
                if (refreshed is not null)
                {
                    return refreshed.AccessToken;
                }
            }

            if (!allowPairing)
            {
                throw new CompanionApiException(HttpStatusCode.Unauthorized, "desktop_pairing_required");
            }

            var paired = await PairAsync(cancellationToken).ConfigureAwait(false);
            return paired.AccessToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task InvalidateAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var stored = await _credentials.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (stored is not null)
            {
                await _credentials.WriteAsync(
                    stored with { ExpiresAt = DateTimeOffset.MinValue },
                    cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var stored = await _credentials.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (stored is not null)
            {
                try
                {
                    await _api.DeleteSessionAsync(stored.AccessToken, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (CompanionApiException)
                {
                    // Local sign-out must still remove credentials if the session already expired.
                }
            }

            await _credentials.DeleteAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<StoredDeviceCredential?> TryRefreshAsync(
        StoredDeviceCredential stored,
        CancellationToken cancellationToken)
    {
        TokenResponse response;
        try
        {
            response = await _api.RequestTokenAsync(
                new TokenRequest("refresh_token", RefreshToken: stored.RefreshToken),
                cancellationToken).ConfigureAwait(false);
        }
        catch (CompanionApiException exception) when (
            exception.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
        {
            await _credentials.DeleteAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        if (!string.IsNullOrWhiteSpace(response.Error)
            || string.IsNullOrWhiteSpace(response.AccessToken))
        {
            await _credentials.DeleteAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        var refreshed = ToStoredCredential(response, stored.RefreshToken, stored.DeviceId);
        await _credentials.WriteAsync(refreshed, cancellationToken).ConfigureAwait(false);
        return refreshed;
    }

    private async Task<StoredDeviceCredential> PairAsync(CancellationToken cancellationToken)
    {
        var verifier = Pkce.CreateVerifier();
        var pairing = await _api.StartPairingAsync(
            new PairingRequest(_deviceName, Pkce.CreateChallenge(verifier)),
            cancellationToken).ConfigureAwait(false);
        await _pairingInteraction.PairingStartedAsync(pairing, cancellationToken).ConfigureAwait(false);

        var interval = TimeSpan.FromSeconds(Math.Clamp(pairing.PollIntervalSeconds, 1, 30));
        var deadline = _timeProvider.GetUtcNow().AddSeconds(Math.Max(1, pairing.ExpiresInSeconds));
        while (_timeProvider.GetUtcNow() < deadline)
        {
            await Task.Delay(interval, _timeProvider, cancellationToken).ConfigureAwait(false);
            var response = await _api.RequestTokenAsync(
                new TokenRequest(
                    "device_code",
                    DeviceCode: pairing.DeviceCode,
                    CodeVerifier: verifier),
                cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(response.Error)
                && !string.IsNullOrWhiteSpace(response.AccessToken)
                && !string.IsNullOrWhiteSpace(response.RefreshToken))
            {
                var stored = ToStoredCredential(response, response.RefreshToken, response.DeviceId);
                await _credentials.WriteAsync(stored, cancellationToken).ConfigureAwait(false);
                return stored;
            }

            switch (response.Error)
            {
                case "authorization_pending":
                    continue;
                case "slow_down":
                    interval += TimeSpan.FromSeconds(5);
                    continue;
                case "access_denied":
                    throw new InvalidOperationException("pairing_access_denied");
                case "expired_token":
                    throw new InvalidOperationException("pairing_expired");
                default:
                    throw new InvalidOperationException(response.Error ?? "pairing_failed");
            }
        }

        throw new TimeoutException("pairing_expired");
    }

    private StoredDeviceCredential ToStoredCredential(
        TokenResponse response,
        string fallbackRefreshToken,
        string? fallbackDeviceId)
    {
        var lifetime = TimeSpan.FromSeconds(Math.Max(1, response.ExpiresInSeconds));
        return new StoredDeviceCredential(
            response.AccessToken!,
            string.IsNullOrWhiteSpace(response.RefreshToken)
                ? fallbackRefreshToken
                : response.RefreshToken,
            _timeProvider.GetUtcNow() + lifetime,
            response.DeviceId ?? fallbackDeviceId);
    }
}

