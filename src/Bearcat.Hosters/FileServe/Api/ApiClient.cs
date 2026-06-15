using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Shared.XFilesharing.Api;

namespace Bearcat.Hosters.FileServe.Api;

public class ApiClient(IFileServeApi api, HttpClientProvider httpClientProvider)
    : XFilesharingApiClient<IFileServeApi>(
        api,
        httpClientProvider,
        new XFilesharingUploadOptions(
            AddRegisteredUserTypeField: false,
            AddUploadTypeQueryString: false,
            ForceHttpUploadScheme: false
        )
    ),
        IFileServeApiClient
{
    public const string ApiBaseUrl = "https://fileserve.com/api";
}
