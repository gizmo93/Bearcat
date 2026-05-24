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
}
