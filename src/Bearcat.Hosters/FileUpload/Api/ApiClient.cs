using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Shared.XFilesharing.Api;

namespace Bearcat.Hosters.FileUpload.Api;

public class ApiClient(IFileUploadApi api, HttpClientProvider httpClientProvider)
    : XFilesharingApiClient<IFileUploadApi>(
        api,
        httpClientProvider,
        new XFilesharingUploadOptions(
            AddRegisteredUserTypeField: true,
            AddUploadTypeQueryString: false,
            ForceHttpUploadScheme: false
        )
    ),
        IFileUploadApiClient
{
    public const string ApiBaseUrl = "https://file-upload.org/api";
}
