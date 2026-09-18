using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Shared.XFilesharing.Api;

namespace Bearcat.Hosters.ClicknUpload.Api;

public class ApiClient(
    IClicknUploadApi api,
    HttpClientProvider httpClientProvider,
    HosterFileDownloader fileDownloader
)
    : XFilesharingApiClient<IClicknUploadApi>(
        api,
        httpClientProvider,
        new XFilesharingUploadOptions(
            AddRegisteredUserTypeField: true,
            AddUploadTypeQueryString: false,
            ForceHttpUploadScheme: false
        ),
        fileDownloader
    ),
        IClicknUploadApiClient
{
    public const string ApiBaseUrl = "https://clicknupload.click/api";
}
