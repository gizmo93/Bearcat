using System.Text.Json;
using System.Text.Json.Serialization;
using BearCat.Core.Infrastructure.Hosters.Rapidgator.ApiClient.File;
using BearCat.Core.Infrastructure.Hosters.Rapidgator.ApiClient.User;

namespace BearCat.Core.Infrastructure.Hosters.Rapidgator.ApiClient;

public class RapidgatorApiClient(
    IRapidgatorApi api,
    IHttpClientFactory httpClientFactory)
{
    public async Task<LoginResponse> LoginAsync(string login, string password, CancellationToken cancellationToken)
    {
        var response = await api.LoginAsync(login, password, cancellationToken);
        return response.Content!;
    }

    public async Task<UploadFileResponse> RequestUploadFileAsync(
        string token,
        string name,
        long size,
        string hash,
        CancellationToken cancellationToken)
    {
        var response = await api.RequestUploadFileAsync(token, name, size, hash, cancellationToken);
        return response.Content!;
    }

    public async Task<UploadFileResponse> UploadFileAsync(
        string uploadUrl,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient();
        var response = await httpClient.PostAsync(uploadUrl,
            new MultipartFormDataContent { { new StreamContent(stream), "file", fileName } }, cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        return JsonSerializer.Deserialize<UploadFileResponse>(content,
            options: new JsonSerializerOptions
            {
                NumberHandling = JsonNumberHandling.AllowReadingFromString, PropertyNameCaseInsensitive = true,
            })!;
    }

    public async Task<UploadFileResponse> GetUploadInfoAsync(
        string token,
        string uploadId,
        CancellationToken cancellationToken)
    {
        var response = await api.GetFileStatusAsync(token, uploadId, cancellationToken);
        return response.Content!;
    }
}
