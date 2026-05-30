using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Hosters.Extensions;

namespace Bearcat.Hosters.Shared.XFilesharing.Api;

public abstract class XFilesharingApiClient<TApi>(
    TApi api,
    HttpClientProvider httpClientProvider,
    XFilesharingUploadOptions uploadOptions
) : IXFilesharingApiClient
    where TApi : IXFilesharingApi
{
    public async Task<Dictionary<string, bool>> FilesExistAsync(
        string apiKey,
        IReadOnlySet<string> fileCodes,
        CancellationToken cancellationToken
    )
    {
        var result = fileCodes.ToDictionary(fileCode => fileCode, _ => false);

        foreach (var batch in fileCodes.Chunk(50))
        {
            var response = await api.GetFileInfoAsync(
                apiKey,
                string.Join(',', batch),
                cancellationToken
            );

            foreach (var file in response.Results.Where(file => file.FileCode is not null))
            {
                result[file.FileCode!] = file.Status == (int)HttpStatusCode.OK;
            }
        }

        return result;
    }

    public async Task<AccountInfoResponse> GetAccountInfoAsync(
        string apiKey,
        CancellationToken cancellationToken
    )
    {
        return await api.GetAccountInfoAsync(apiKey, cancellationToken);
    }

    public async Task<RequestUploadResponse> RequestUploadAsync(
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

    public async Task<UploadFileResponse> UploadFileAsync(
        Stream stream,
        string fileName,
        string uploadUrl,
        string sessionId,
        CancellationToken cancellationToken
    )
    {
        using var httpClient = httpClientProvider.GetUploadClient();

        using var multipartForm = new MultipartFormDataContent();

        var sessionIdContent = new StringContent(sessionId);
        sessionIdContent.Headers.ContentType = null;
        sessionIdContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = "\"sess_id\"",
        };
        multipartForm.Add(sessionIdContent, "sess_id");

        if (uploadOptions.AddRegisteredUserTypeField)
        {
            var userTypeContent = new StringContent(uploadOptions.UserTypeFieldValue);
            userTypeContent.Headers.ContentType = null;
            userTypeContent.Headers.ContentDisposition = new ContentDispositionHeaderValue(
                "form-data"
            )
            {
                Name = "\"utype\"",
            };
            multipartForm.Add(userTypeContent, "utype");
        }

        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = "\"file\"",
            FileName = $"\"{fileName}\"",
        };

        multipartForm.Add(fileContent);

        uploadUrl = PrepareUploadUrl(uploadUrl);

        var httpResponse = await httpClient.PostAsync(uploadUrl, multipartForm, cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Upload request failed with status code {httpResponse.StatusCode} for file {fileName}"
            );
        }

        var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        var response = JsonSerializer.Deserialize<List<UploadFileResponse>>(
            content,
            options: new JsonSerializerOptions
            {
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                PropertyNameCaseInsensitive = true,
            }
        )!;

        return response.First();
    }

    public async Task<string> CreateFolderAsync(
        string apiKey,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        var rootFolder = await api.GetFolderListAsync(
            apiKey: apiKey,
            folderId: null,
            cancellationToken: cancellationToken
        );

        EnsureSuccess(rootFolder.Status, rootFolder.Msg, "XFilesharing folder list failed");

        var existingFolder = rootFolder.Result?.Folders.FirstOrDefault(folder =>
            string.Equals(folder.Name, folderName, StringComparison.Ordinal)
        );

        if (existingFolder is not null)
        {
            return existingFolder.FolderId;
        }

        var createdFolder = await api.CreateFolderAsync(
            apiKey: apiKey,
            name: folderName,
            cancellationToken: cancellationToken
        );

        EnsureSuccess(
            createdFolder.Status,
            createdFolder.Msg,
            "XFilesharing folder creation failed"
        );

        if (string.IsNullOrWhiteSpace(createdFolder.Result?.FolderId))
        {
            throw new InvalidOperationException(
                $"{nameof(GetType)} folder creation returned no folder id"
            );
        }

        return createdFolder.Result.FolderId;
    }

    public async Task SetFileFolderAsync(
        string apiKey,
        string fileCode,
        string folderId,
        CancellationToken cancellationToken
    )
    {
        var response = await api.SetFileFolderAsync(
            apiKey: apiKey,
            fileCode: fileCode,
            folderId: folderId,
            cancellationToken: cancellationToken
        );

        EnsureSuccess(
            response.Status,
            response.Msg,
            $"{nameof(GetType)} file folder update failed"
        );
    }

    private string PrepareUploadUrl(string uploadUrl)
    {
        if (uploadOptions.AddUploadTypeQueryString)
        {
            var separator = uploadUrl.Contains('?') ? '&' : '?';
            uploadUrl = $"{uploadUrl}{separator}upload_type=file&utype=reg";
        }

        return uploadOptions.ForceHttpUploadScheme
            ? uploadUrl.Replace("https://", "http://", StringComparison.OrdinalIgnoreCase)
            : uploadUrl;
    }

    private static void EnsureSuccess(int status, string? message, string errorPrefix)
    {
        if (!((HttpStatusCode)status).IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"{errorPrefix}: {message}");
        }
    }
}
