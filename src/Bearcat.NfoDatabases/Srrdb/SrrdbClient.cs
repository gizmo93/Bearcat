using Bearcat.Abstractions.NfoDatabase;
using Bearcat.NfoDatabases.Srrdb.Api;

namespace Bearcat.NfoDatabases.Srrdb;

public class SrrdbClient(ISrrdbApi api, IHttpClientFactory httpClientFactory)
{
    private const string DownloadHttpClientName = "SrrdbNfoDownload";

    public async Task<SrrdbDetailsResponse?> GetDetailsAsync(
        string releaseName,
        CancellationToken cancellationToken = default
    )
    {
        var response = await api.GetDetailsAsync(releaseName, cancellationToken);
        return response.IsSuccessStatusCode ? response.Content : null;
    }

    public async Task<SrrdbImdbResponse?> GetImdbAsync(
        string releaseName,
        CancellationToken cancellationToken = default
    )
    {
        var response = await api.GetImdbAsync(releaseName, cancellationToken);
        return response.IsSuccessStatusCode ? response.Content : null;
    }

    public async Task<SrrdbNfoResponse?> GetNfoAsync(
        string releaseName,
        CancellationToken cancellationToken = default
    )
    {
        var response = await api.GetNfoAsync(releaseName, cancellationToken);
        return response.IsSuccessStatusCode ? response.Content : null;
    }

    public async Task<string?> DownloadNfoAsync(
        string downloadUrl,
        CancellationToken cancellationToken = default
    )
    {
        var httpClient = httpClientFactory.CreateClient(DownloadHttpClientName);
        var bytes = await httpClient.GetByteArrayAsync(downloadUrl, cancellationToken);
        return NfoTextDecoder.Decode(bytes);
    }
}
