using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Shared.XFilesharing.Api;

namespace Bearcat.Hosters.FileQ.Api;

public class ApiClient(
    IFileQApi api,
    HttpClientProvider httpClientProvider,
    HosterFileDownloader fileDownloader
)
    : XFilesharingApiClient<IFileQApi>(
        api,
        httpClientProvider,
        new XFilesharingUploadOptions(
            AddRegisteredUserTypeField: true,
            AddUploadTypeQueryString: false,
            ForceHttpUploadScheme: false,
            UserTypeFieldValue: "prem"
        ),
        fileDownloader
    ),
        IFileQApiClient
{
    public const string ApiBaseUrl = "https://fileq.net/api";
}
