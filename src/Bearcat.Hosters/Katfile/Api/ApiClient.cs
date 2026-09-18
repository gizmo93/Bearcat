using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Shared.XFilesharing.Api;

namespace Bearcat.Hosters.Katfile.Api;

public class ApiClient(
    IKatfileApi api,
    HttpClientProvider httpClientProvider,
    HosterFileDownloader fileDownloader
)
    : XFilesharingApiClient<IKatfileApi>(
        api,
        httpClientProvider,
        new XFilesharingUploadOptions(
            AddRegisteredUserTypeField: false,
            AddUploadTypeQueryString: false,
            ForceHttpUploadScheme: false
        ),
        fileDownloader
    ),
        IKatfileApiClient
{
    public const string ApiBaseUrl = "https://www.katfile.com/api";
}
