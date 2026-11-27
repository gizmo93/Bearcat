using System.Net;
using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Shared.XFilesharing.Api;

namespace Bearcat.Hosters.DDownload.Api;

public class ApiClient(
    IDDownloadApi api,
    HttpClientProvider httpClientProvider,
    HosterFileDownloader fileDownloader
)
    : XFilesharingApiClient<IDDownloadApi>(
        api,
        httpClientProvider,
        new XFilesharingUploadOptions(
            AddRegisteredUserTypeField: true,
            AddUploadTypeQueryString: true,
            ForceHttpUploadScheme: true
        ),
        fileDownloader
    ),
        IDDownloadApiClient
{
    public const string ApiBaseUrl = "https://api-v2.ddownload.com/api";

    private const int FileCheckBatchSize = 500;

    public override async Task<Dictionary<string, XFilesharingFileStatus>> FilesExistAsync(
        string apiKey,
        IReadOnlySet<string> fileCodes,
        CancellationToken cancellationToken
    )
    {
        var result = new Dictionary<string, XFilesharingFileStatus>();

        foreach (var batch in fileCodes.Chunk(FileCheckBatchSize))
        {
            var response = await SendApiRequestAsync(() =>
                Api.CheckFilesAsync(
                    apiKey: apiKey,
                    fileCodes: string.Join(',', batch),
                    cancellationToken: cancellationToken
                )
            );

            if (response.Status != (int)HttpStatusCode.OK)
            {
                XFilesharingApiKeyRejection.ThrowIfRejected(response.Status, response.Msg);

                throw new HttpRequestException(
                    $"DDownload file check request failed with status {response.Status}: {response.Msg}"
                );
            }

            foreach (var file in response.Result.Files.Where(file => file.FileCode is not null))
            {
                result[file.FileCode!] = new XFilesharingFileStatus(
                    Exists: file.Status == (int)HttpStatusCode.OK,
                    DownloadCount: ParseDownloadCount(file.Downloads)
                );
            }
        }

        return result;
    }
}
