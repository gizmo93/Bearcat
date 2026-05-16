namespace Bearcat.Hosters.Shared;

public class HttpClientProvider(IHttpClientFactory httpClientFactory)
{
    public const string UploadHttpClientName = "UploadHttpClient";

    public HttpClient GetUploadClient() => httpClientFactory.CreateClient(UploadHttpClientName);
}
