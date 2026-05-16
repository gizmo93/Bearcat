using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Hosters.DDownload.Api.AccountInfo;
using Bearcat.Hosters.Shared;

namespace Bearcat.Hosters.DDownload.Api;

public class ApiClient(IDDownloadApi api, HttpClientProvider httpClientProvider)
{
    public const string ApiBaseUrl = "https://api-v2.ddownload.com/api";

    public async Task<Dictionary<string, bool>> FilesExistAsync(
        string apiKey,
        IReadOnlySet<string> fileCodes,
        CancellationToken cancellationToken
    )
    {
        var result = new Dictionary<string, bool>();

        foreach (var batch in fileCodes.Chunk(100))
        {
            var response = await api.CheckFilesExistAsync(
                apiKey,
                batch.ToHashSet(),
                cancellationToken
            );

            foreach (var file in response.Result.Files)
            {
                result[file.FileCode] = file.Status == (int)HttpStatusCode.OK;
            }
        }

        return result;
    }

    public async Task<Response> GetAccountInfoAsync(
        string apiKey,
        CancellationToken cancellationToken
    )
    {
        return await api.GetAccountInfoAsync(apiKey, cancellationToken);
    }

    public async Task<RequestUpload.Response> RequestUploadAsync(
        string apiKey,
        CancellationToken cancellationToken
    )
    {
        var response = await api.RequestUploadAsync(apiKey, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Request upload server failed with status code {response.StatusCode}"
            );
        }

        return response.Content!;
    }

    public async Task<UploadFile.Response> UploadFileAsync(
        Stream stream,
        string fileName,
        string uploadUrl,
        string sessionId,
        CancellationToken cancellationToken
    )
    {
        using var httpClient = httpClientProvider.GetUploadClient();

        using var multipartForm = new MultipartFormDataContent();

        var sessIdContent = new StringContent(sessionId);
        sessIdContent.Headers.ContentType = null;
        sessIdContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = "\"sess_id\"",
        };
        multipartForm.Add(sessIdContent, "sess_id");

        var utypeContent = new StringContent("reg");
        utypeContent.Headers.ContentType = null;
        utypeContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = "\"utype\"",
        };
        multipartForm.Add(utypeContent, "utype");

        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = "\"file\"",
            FileName = $"\"{fileName}\"",
        };

        multipartForm.Add(fileContent);

        uploadUrl = $"{uploadUrl}?upload_type=file&utype=reg";
        uploadUrl = uploadUrl.Replace("https://", "http://");

        var httpResponse = await httpClient.PostAsync(uploadUrl, multipartForm, cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Upload request failed with status code {httpResponse.StatusCode} for file {fileName}"
            );
        }

        var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        var response = JsonSerializer.Deserialize<List<UploadFile.Response>>(
            content,
            options: new JsonSerializerOptions
            {
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                PropertyNameCaseInsensitive = true,
            }
        )!;

        return response.First();
    }
}
