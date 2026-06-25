using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Shared.XFilesharing.Api;

namespace Bearcat.Hosters.DataVaults.Api;

public class ApiClient(IDataVaultsApi api, HttpClientProvider httpClientProvider)
    : XFilesharingApiClient<IDataVaultsApi>(
        api,
        httpClientProvider,
        new XFilesharingUploadOptions(
            AddRegisteredUserTypeField: true,
            AddUploadTypeQueryString: false,
            ForceHttpUploadScheme: false
        )
    ),
        IDataVaultsApiClient
{
    public const string ApiBaseUrl = "https://datavaults.co/api";
}
