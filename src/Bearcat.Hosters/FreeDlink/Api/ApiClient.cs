using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Shared.XFilesharing.Api;

namespace Bearcat.Hosters.FreeDlink.Api;

public class ApiClient(IFreeDlinkApi api, HttpClientProvider httpClientProvider)
    : XFilesharingApiClient<IFreeDlinkApi>(
        api,
        httpClientProvider,
        new XFilesharingUploadOptions(
            AddRegisteredUserTypeField: false,
            AddUploadTypeQueryString: false,
            ForceHttpUploadScheme: false
        )
    ),
        IFreeDlinkApiClient
{
    public const string ApiBaseUrl = "https://freedl.ink/api";
}
