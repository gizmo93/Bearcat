using Bearcat.Abstractions.NfoDatabase;
using Bearcat.NfoDatabases.Predb.Api;
using Microsoft.Extensions.Logging;

namespace Bearcat.NfoDatabases.Predb;

public class PredbClient(
    IPredbApi api,
    PredbRateLimiter rateLimiter,
    PredbDownloadQuota downloadQuota,
    IHttpClientFactory httpClientFactory,
    ILogger<PredbClient> logger
)
{
    public const string DownloadHttpClientName = "PredbNfoDownload";

    private const string DownloadLimitPhrase = "daily download limit";
    private const int ShortestPlausibleNfoLength = 200;

    public async Task<PredbReleaseResponse?> GetPreAsync(
        string releaseName,
        CancellationToken cancellationToken = default
    )
    {
        await rateLimiter.WaitForSlotAsync(cancellationToken);

        var response = await api.GetPreAsync(releaseName, cancellationToken);
        return response.IsSuccessStatusCode ? response.Content?.Data?.FirstOrDefault() : null;
    }

    public async Task<PredbNfoData?> GetNfoAsync(
        string releaseName,
        CancellationToken cancellationToken = default
    )
    {
        if (downloadQuota.IsExhausted())
        {
            return null;
        }

        await rateLimiter.WaitForSlotAsync(cancellationToken);

        var response = await api.GetNfoAsync(releaseName, cancellationToken);
        return response.IsSuccessStatusCode ? response.Content?.Data : null;
    }

    public async Task<string?> DownloadNfoAsync(
        string downloadUrl,
        CancellationToken cancellationToken = default
    )
    {
        var httpClient = httpClientFactory.CreateClient(DownloadHttpClientName);
        var bytes = await httpClient.GetByteArrayAsync(downloadUrl, cancellationToken);
        var content = NfoTextDecoder.Decode(bytes);

        if (!IsDownloadLimitMessage(content))
        {
            return content;
        }

        var exhaustedUntil = downloadQuota.MarkExhausted();
        logger.LogWarning(
            "PreDB daily NFO download limit reached, skipping PreDB NFO downloads until {ExhaustedUntil}",
            exhaustedUntil
        );

        return null;
    }

    private static bool IsDownloadLimitMessage(string content)
    {
        return content.Length < ShortestPlausibleNfoLength
            && content.Contains(DownloadLimitPhrase, StringComparison.OrdinalIgnoreCase);
    }
}
