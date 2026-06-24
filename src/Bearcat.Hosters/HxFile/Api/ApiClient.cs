using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Shared.XFilesharing.Api;

namespace Bearcat.Hosters.HxFile.Api;

public class ApiClient(IHxFileApi api, HttpClientProvider httpClientProvider)
    : XFilesharingApiClient<IHxFileApi>(
        api,
        httpClientProvider,
        new XFilesharingUploadOptions(
            AddRegisteredUserTypeField: false,
            AddUploadTypeQueryString: false,
            ForceHttpUploadScheme: false
        )
    ),
        IHxFileApiClient
{
    public const string ApiBaseUrl = "https://hxfile.co/api";
}
