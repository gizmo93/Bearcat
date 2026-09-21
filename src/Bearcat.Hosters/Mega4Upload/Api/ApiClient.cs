using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Shared.XFilesharing.Api;

namespace Bearcat.Hosters.Mega4Upload.Api;

public class ApiClient(
    IMega4UploadApi api,
    HttpClientProvider httpClientProvider,
    HosterFileDownloader fileDownloader
)
    : XFilesharingApiClient<IMega4UploadApi>(
        api,
        httpClientProvider,
        new XFilesharingUploadOptions(
            AddRegisteredUserTypeField: false,
            AddUploadTypeQueryString: false,
            ForceHttpUploadScheme: false
        ),
        fileDownloader
    ),
        IMega4UploadApiClient
{
    public const string ApiBaseUrl = "https://mega4upload.net/api";

    protected override MultipartFormDataContent AddHosterSpecificUploadFields(
        MultipartFormDataContent multipartForm
    )
    {
        AddFormField(multipartForm, name: "ajax", value: "1");

        return multipartForm;
    }
}
