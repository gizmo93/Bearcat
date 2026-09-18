using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Shared.XFilesharing.Api;

namespace Bearcat.Hosters.FreeDlink.Api;

public class ApiClient(
    IFreeDlinkApi api,
    HttpClientProvider httpClientProvider,
    HosterFileDownloader fileDownloader
)
    : XFilesharingApiClient<IFreeDlinkApi>(
        api,
        httpClientProvider,
        new XFilesharingUploadOptions(
            AddRegisteredUserTypeField: false,
            AddUploadTypeQueryString: false,
            ForceHttpUploadScheme: false
        ),
        fileDownloader
    ),
        IFreeDlinkApiClient
{
    public const string ApiBaseUrl = "https://freedl.ink/api";
}
