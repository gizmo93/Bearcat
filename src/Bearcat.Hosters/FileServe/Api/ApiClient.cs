using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Shared.XFilesharing.Api;

namespace Bearcat.Hosters.FileServe.Api;

public class ApiClient(
    IFileServeApi api,
    HttpClientProvider httpClientProvider,
    HosterFileDownloader fileDownloader
)
    : XFilesharingApiClient<IFileServeApi>(
        api,
        httpClientProvider,
        new XFilesharingUploadOptions(
            AddRegisteredUserTypeField: false,
            AddUploadTypeQueryString: false,
            ForceHttpUploadScheme: false
        ),
        fileDownloader
    ),
        IFileServeApiClient
{
    public const string ApiBaseUrl = "https://fileserve.com/api";
}
