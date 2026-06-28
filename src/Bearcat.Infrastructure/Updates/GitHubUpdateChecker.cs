using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Updates;

namespace Bearcat.Infrastructure.Updates;

public sealed class GitHubUpdateChecker(
    IHttpClientFactory httpClientFactory,
    IAppVersionProvider appVersionProvider
) : IUpdateChecker, IDisposable
{
    public const string HttpClientName = "github-updates";

    private const string Owner = "gizmo93";
    private const string Repository = "Bearcat";

    private static readonly Uri LatestReleaseUri = new(
        $"https://api.github.com/repos/{Owner}/{Repository}/releases/latest"
    );

    private static readonly string ReleasesPageUrl =
        $"https://github.com/{Owner}/{Repository}/releases";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

    private readonly SemaphoreSlim refreshLock = new(1, 1);

    private UpdateStatus? cachedStatus;
    private DateTimeOffset cachedAt;

    public string CurrentVersion { get; } = appVersionProvider.CurrentVersion;

    public async Task<UpdateStatus> GetUpdateStatusAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (TryGetFreshStatus(out var status))
        {
            return status;
        }

        await refreshLock.WaitAsync(cancellationToken);

        try
        {
            if (TryGetFreshStatus(out status))
            {
                return status;
            }

            cachedStatus = await FetchStatusAsync(cancellationToken);
            cachedAt = DateTimeOffset.UtcNow;
            return cachedStatus;
        }
        finally
        {
            refreshLock.Release();
        }
    }

    public void Dispose()
    {
        refreshLock.Dispose();
    }

    private bool TryGetFreshStatus(out UpdateStatus status)
    {
        if (cachedStatus is not null && DateTimeOffset.UtcNow - cachedAt < CacheDuration)
        {
            status = cachedStatus;
            return true;
        }

        status = null!;
        return false;
    }

    private async Task<UpdateStatus> FetchStatusAsync(CancellationToken cancellationToken)
    {
        if (IsDevelopmentVersion(CurrentVersion))
        {
            return new UpdateStatus(CurrentVersion, null, false, ReleasesPageUrl);
        }

        try
        {
            using var httpClient = httpClientFactory.CreateClient(HttpClientName);
            var release = await httpClient.GetFromJsonAsync<GitHubRelease>(
                requestUri: LatestReleaseUri,
                cancellationToken: cancellationToken
            );

            if (string.IsNullOrWhiteSpace(release?.TagName))
            {
                return new UpdateStatus(CurrentVersion, null, false, ReleasesPageUrl);
            }

            var latestVersion = NormalizeVersion(release.TagName);
            var releaseUrl = string.IsNullOrWhiteSpace(release.HtmlUrl)
                ? ReleasesPageUrl
                : release.HtmlUrl;

            return new UpdateStatus(
                CurrentVersion: CurrentVersion,
                LatestVersion: latestVersion,
                IsUpdateAvailable: IsNewer(latestVersion, CurrentVersion),
                ReleaseUrl: releaseUrl
            );
        }
        catch (Exception exception)
            when (exception
                    is HttpRequestException
                        or TaskCanceledException
                        or NotSupportedException
                        or JsonException
            )
        {
            return new UpdateStatus(CurrentVersion, null, false, ReleasesPageUrl);
        }
    }

    private static bool IsDevelopmentVersion(string version)
    {
        return version.StartsWith("0.0.0", StringComparison.Ordinal);
    }

    private static string NormalizeVersion(string tag) => tag.TrimStart('v', 'V');

    private static bool IsNewer(string latest, string current)
    {
        var latestVersion = ParseOrNull(latest);
        var currentVersion = ParseOrNull(current);

        return latestVersion is not null
            && currentVersion is not null
            && latestVersion.CompareTo(currentVersion) > 0;
    }

    private static Version? ParseOrNull(string value)
    {
        // Drop any pre-release / build suffix before parsing (e.g. "1.2.3-beta").
        var core = value.Split('-', '+')[0];
        return Version.TryParse(core, out var version) ? version : null;
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }
    }
}
