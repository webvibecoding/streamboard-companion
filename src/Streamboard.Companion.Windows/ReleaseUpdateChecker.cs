using System.Net.Http.Json;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Streamboard.Companion.Windows;

internal sealed record UpdateCheckResult(bool Checked, bool UpdateAvailable, string? Version, Uri? ReleaseUri);

internal sealed class ReleaseUpdateChecker
{
    private static readonly Uri LatestReleaseUri = new(
        "https://api.github.com/repos/webvibecoding/streamboard-companion/releases/latest");
    private static readonly TimeSpan AutomaticInterval = TimeSpan.FromDays(1);

    private readonly HttpClient _httpClient;
    private readonly string _statePath;
    private readonly TimeProvider _timeProvider;

    public ReleaseUpdateChecker(HttpClient httpClient, string statePath, TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient;
        _statePath = statePath;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<UpdateCheckResult> CheckAsync(
        bool force,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var lastCheck = await ReadLastCheckAsync(cancellationToken).ConfigureAwait(false);
        if (!force && lastCheck.HasValue && now - lastCheck.Value < AutomaticInterval)
        {
            return new UpdateCheckResult(false, false, null, null);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUri);
            request.Headers.UserAgent.ParseAdd($"Streamboard.Companion/{App.AppVersion.ToString(3)}");
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var releaseVersion = ParseVersion(release?.TagName);
            var currentVersion = App.AppVersion;
            var releaseUri = Uri.TryCreate(release?.HtmlUrl, UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps
                    ? uri
                    : null;
            var available = releaseVersion is not null
                && releaseUri is not null
                && releaseVersion > currentVersion;
            return new UpdateCheckResult(
                true,
                available,
                release?.TagName?.TrimStart('v', 'V'),
                releaseUri);
        }
        finally
        {
            await WriteLastCheckAsync(now, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<DateTimeOffset?> ReadLastCheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(_statePath);
            var state = await JsonSerializer.DeserializeAsync<UpdateState>(
                stream,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return state?.LastCheckedAt;
        }
        catch (Exception exception) when (
            exception is FileNotFoundException
                or DirectoryNotFoundException
                or IOException
                or UnauthorizedAccessException
                or JsonException)
        {
            return null;
        }
    }

    private async Task WriteLastCheckAsync(DateTimeOffset checkedAt, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
            await using var stream = new FileStream(
                _statePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous);
            await JsonSerializer.SerializeAsync(
                stream,
                new UpdateState(checkedAt),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Update checks remain non-critical if the timestamp cannot be persisted.
        }
    }

    private static Version? ParseVersion(string? tag)
    {
        var normalized = tag?.Trim().TrimStart('v', 'V').Split('-', 2)[0];
        return Version.TryParse(normalized, out var version) ? version : null;
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string? TagName,
        [property: JsonPropertyName("html_url")] string? HtmlUrl);

    private sealed record UpdateState(DateTimeOffset LastCheckedAt);
}
