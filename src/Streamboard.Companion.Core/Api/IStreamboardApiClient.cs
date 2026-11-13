using System.Net;

namespace Streamboard.Companion.Core.Api;

public interface IStreamboardApiClient
{
    Task<PairingResponse> StartPairingAsync(
        PairingRequest request,
        CancellationToken cancellationToken = default);

    Task<TokenResponse> RequestTokenAsync(
        TokenRequest request,
        CancellationToken cancellationToken = default);

    Task<BootstrapResponse> GetBootstrapAsync(
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<SyncResponse> SyncAsync(
        string accessToken,
        SyncRequest request,
        CancellationToken cancellationToken = default);

    Task<CategorySearchResponse> SearchCategoriesAsync(
        string accessToken,
        string platformId,
        string query,
        CancellationToken cancellationToken = default);

    Task<MappingListResponse> GetMappingsAsync(
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<DesktopMapping> PutMappingAsync(
        string accessToken,
        DesktopMapping mapping,
        CancellationToken cancellationToken = default);

    Task DeleteMappingAsync(
        string accessToken,
        string gameKey,
        string platformId,
        CancellationToken cancellationToken = default);

    Task DeleteSessionAsync(
        string accessToken,
        CancellationToken cancellationToken = default);
}

public sealed class CompanionApiException : Exception
{
    public CompanionApiException(HttpStatusCode statusCode, string reason)
        : base(reason)
    {
        StatusCode = statusCode;
        Reason = reason;
    }

    public HttpStatusCode StatusCode { get; }

    public string Reason { get; }
}

