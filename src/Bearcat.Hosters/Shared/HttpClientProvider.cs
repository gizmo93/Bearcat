namespace Bearcat.Hosters.Shared;

public class HttpClientProvider(IHttpClientFactory httpClientFactory)
{
    public const string UploadHttpClientName = "UploadHttpClient";

    public const string DownloadHttpClientName = "DownloadHttpClient";

    public HttpClient GetUploadClient() => httpClientFactory.CreateClient(UploadHttpClientName);

    public HttpClient GetDownloadClient() => httpClientFactory.CreateClient(DownloadHttpClientName);

    public HttpClient GetClient(string name) => httpClientFactory.CreateClient(name);
}
