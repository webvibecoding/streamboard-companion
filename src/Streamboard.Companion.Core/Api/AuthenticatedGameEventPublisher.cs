using System.Net;
using Streamboard.Companion.Core.Auth;
using Streamboard.Companion.Core.Detection;
using Streamboard.Companion.Core.Tracking;

namespace Streamboard.Companion.Core.Api;

public sealed class AuthenticatedGameEventPublisher : IGameEventPublisher
{
    private readonly IStreamboardApiClient _api;
    private readonly DeviceTokenManager _tokens;

    public AuthenticatedGameEventPublisher(
        IStreamboardApiClient api,
        DeviceTokenManager tokens)
    {
        _api = api;
        _tokens = tokens;
    }

    public async Task<SyncResponse> PublishAsync(
        ResolvedGame game,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default)
    {
        var request = new SyncRequest(
            Guid.NewGuid().ToString("N"),
            observedAt,
            new SyncGame(
                game.Key,
                game.Source,
                game.DisplayName,
                Path.GetFileName(game.ExecutableName)));

        var accessToken = await _tokens.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await _api.SyncAsync(accessToken, request, cancellationToken).ConfigureAwait(false);
        }
        catch (CompanionApiException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            await _tokens.InvalidateAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            accessToken = await _tokens.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            return await _api.SyncAsync(accessToken, request, cancellationToken).ConfigureAwait(false);
        }
    }
}

