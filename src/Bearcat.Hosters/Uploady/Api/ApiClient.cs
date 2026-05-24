using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Shared.XFilesharing.Api;

namespace Bearcat.Hosters.Uploady.Api;

public class ApiClient(IUploadyApi api, HttpClientProvider httpClientProvider)
    : XFilesharingApiClient<IUploadyApi>(
        api,
        httpClientProvider,
        new XFilesharingUploadOptions(
            AddRegisteredUserTypeField: false,
            AddUploadTypeQueryString: false,
            ForceHttpUploadScheme: false
        )
    ),
        IUploadyApiClient
{
    public const string ApiBaseUrl = "https://uploady.io/api";
}
