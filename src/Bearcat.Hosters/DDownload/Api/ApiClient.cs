using System.Net;
using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Shared.XFilesharing.Api;

namespace Bearcat.Hosters.DDownload.Api;

public class ApiClient(IDDownloadApi api, HttpClientProvider httpClientProvider)
    : XFilesharingApiClient<IDDownloadApi>(
        api,
        httpClientProvider,
        new XFilesharingUploadOptions(
            AddRegisteredUserTypeField: true,
            AddUploadTypeQueryString: true,
            ForceHttpUploadScheme: true
        )
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
        var result = fileCodes.ToDictionary(
            fileCode => fileCode,
            _ => new XFilesharingFileStatus(Exists: false, DownloadCount: null)
        );

        foreach (var batch in fileCodes.Chunk(FileCheckBatchSize))
        {
            var response = await Api.CheckFilesAsync(
                apiKey: apiKey,
                fileCodes: string.Join(',', batch),
                cancellationToken: cancellationToken
            );

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
