using System.Net;
using Streamboard.Companion.Core.Auth;

namespace Streamboard.Companion.Core.Api;

public sealed record CategoryMappingRow(SyncGame Game, DesktopMapping? Twitch, DesktopMapping? VkPlayLive)
{
    public string TwitchLabel => Twitch?.Category.Name ?? "Не выбрана";
    public string VkPlayLiveLabel => VkPlayLive?.Category.Name ?? "Не выбрана";
}

public sealed class CategoryMappings(IStreamboardApiClient api, DeviceTokenManager tokens)
{
    public const string Twitch = "twitch";
    public const string VkPlayLive = "vkplaylive";

    public async Task<IReadOnlyList<CategoryMappingRow>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var response = await AuthorizedAsync(api.GetMappingsAsync, cancellationToken).ConfigureAwait(false);
        return response.Items
            .Where(item => item.PlatformId is Twitch or VkPlayLive)
            .GroupBy(item => item.Game.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var twitch = group.FirstOrDefault(item => item.PlatformId == Twitch);
                var vk = group.FirstOrDefault(item => item.PlatformId == VkPlayLive);
                return new CategoryMappingRow(twitch?.Game ?? group.First().Game, twitch, vk);
            })
            .OrderBy(row => row.Game.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(row => row.Game.Key, StringComparer.Ordinal)
            .ToArray();
    }

    public Task<CategorySearchResponse> SearchAsync(
        string platformId, string query, CancellationToken cancellationToken = default) =>
        AuthorizedAsync((token, ct) => api.SearchCategoriesAsync(token, platformId, query, ct), cancellationToken);

    public Task<DesktopMapping> SaveAsync(
        SyncGame game,
        string platformId,
        CategorySearchItem category,
        CancellationToken cancellationToken = default) =>
        AuthorizedAsync(
            (token, ct) => api.PutMappingAsync(
                token,
                new DesktopMapping(game, platformId, category with { Type = category.Type ?? "game" }),
                ct),
            cancellationToken);

    private async Task<T> AuthorizedAsync<T>(
        Func<string, CancellationToken, Task<T>> request, CancellationToken cancellationToken)
    {
        var token = await tokens.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await request(token, cancellationToken).ConfigureAwait(false);
        }
        catch (CompanionApiException exception) when (IsDeviceUnauthorized(exception))
        {
            await tokens.InvalidateAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            token = await tokens.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            return await request(token, cancellationToken).ConfigureAwait(false);
        }
    }

    public static bool IsDeviceUnauthorized(CompanionApiException exception) =>
        exception.StatusCode == HttpStatusCode.Unauthorized
        && !exception.Reason.StartsWith("twitch_", StringComparison.Ordinal)
        && !exception.Reason.StartsWith("vkplaylive_", StringComparison.Ordinal);
}
