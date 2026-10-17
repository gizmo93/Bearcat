using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Streamtape.Api;

public class ApiClient(
    IStreamtapeApi api,
    HttpClientProvider httpClientProvider,
    ILogger<ApiClient> logger
)
{
    public const string ApiBaseUrl = "https://api.streamtape.com";

    private const string MimeType = "application/octet-stream";

    private const string FileFieldName = "file1";

    private const int MaxFileIdsPerFileInfoRequest = 100;

    private const int SuccessStatus = 200;

    private const int CredentialsRejectedStatus = 403;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<UploadedFileResult> UploadFileAsync(
        StreamtapeConfig config,
        Stream stream,
        string fileName,
        string? folderId,
        CancellationToken cancellationToken
    )
    {
        var uploadUrlResponse = await api.GetUploadUrlAsync(
            apiLogin: config.ApiLogin,
            apiKey: config.ApiKey,
            folderId: folderId,
            cancellationToken: cancellationToken
        );
        var uploadUrl = ReadResult<UploadUrlResult>(uploadUrlResponse, "upload URL").UploadUrl;

        using var httpClient = httpClientProvider.GetUploadClient();
        using var multipartContent = new MultipartFormDataContent();

        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(MimeType);
        fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = $"\"{FileFieldName}\"",
            FileName = $"\"{fileName}\"",
        };
        multipartContent.Add(fileContent);

        using var httpResponse = await httpClient.PostAsync(
            uploadUrl,
            multipartContent,
            cancellationToken
        );

        var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Streamtape upload request failed with status code {httpResponse.StatusCode}: {content}",
                inner: null,
                statusCode: httpResponse.StatusCode
            );
        }

        var response =
            JsonSerializer.Deserialize<StreamtapeResponse>(content, JsonSerializerOptions)
            ?? throw new HttpRequestException("Streamtape upload response was empty");

        if (response.Status != SuccessStatus)
        {
            throw new HttpRequestException($"Streamtape upload request failed: {content}");
        }

        return response.Result.Deserialize<UploadedFileResult>(JsonSerializerOptions)
            ?? throw new HttpRequestException(
                $"Streamtape upload response contained no file: {content}"
            );
    }

    public async Task<string> CreateFolderAsync(
        StreamtapeConfig config,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        var rootFolderContentResponse = await api.GetRootFolderContentAsync(
            apiLogin: config.ApiLogin,
            apiKey: config.ApiKey,
            cancellationToken: cancellationToken
        );

        var existingFolder = ReadResult<FolderListResult>(rootFolderContentResponse, "folder list")
            .Folders.FirstOrDefault(folder =>
                string.Equals(folder.Name, folderName, StringComparison.OrdinalIgnoreCase)
            );

        if (existingFolder is not null)
        {
            logger.LogInformation(
                "Reusing existing Streamtape folder {FolderName} with id {FolderId}",
                folderName,
                existingFolder.FolderId
            );

            return existingFolder.FolderId;
        }

        logger.LogInformation("Creating Streamtape folder {FolderName}", folderName);

        var response = await api.CreateFolderAsync(
            apiLogin: config.ApiLogin,
            apiKey: config.ApiKey,
            name: folderName,
            cancellationToken: cancellationToken
        );

        return ReadResult<CreatedFolderResult>(response, "folder creation").FolderId;
    }

    public async Task MoveFileToFolderAsync(
        StreamtapeConfig config,
        string fileUrl,
        string? externalId,
        string folderId,
        CancellationToken cancellationToken
    )
    {
        var fileId =
            externalId
            ?? ExtractFileId(fileUrl)
            ?? throw new HttpRequestException(
                $"Could not extract the Streamtape file id from URL {fileUrl}"
            );

        var response = await api.MoveFileToFolderAsync(
            apiLogin: config.ApiLogin,
            apiKey: config.ApiKey,
            fileId: fileId,
            folderId: folderId,
            cancellationToken: cancellationToken
        );

        EnsureSuccess(response, "file move");
    }

    public async Task<IReadOnlyDictionary<string, bool>> CheckLinksAsync(
        StreamtapeConfig config,
        IReadOnlyList<FileUrlToCheckDto> files,
        CancellationToken cancellationToken
    )
    {
        var fileUrlsPerFileId = files
            .DistinctBy(file => file.Url)
            .Select(file => new { FileId = file.ExternalId ?? ExtractFileId(file.Url), file.Url })
            .Where(entry => entry.FileId is not null)
            .GroupBy(entry => entry.FileId!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(entry => entry.Url).ToList(),
                StringComparer.Ordinal
            );

        var isOnlinePerFileUrl = new Dictionary<string, bool>();

        foreach (var fileIds in fileUrlsPerFileId.Keys.Chunk(MaxFileIdsPerFileInfoRequest))
        {
            var response = await api.GetFileInfoAsync(
                apiLogin: config.ApiLogin,
                apiKey: config.ApiKey,
                fileIds: string.Join(',', fileIds),
                cancellationToken: cancellationToken
            );

            var fileInfosPerFileId = ReadResult<Dictionary<string, FileInfoResult>>(
                response: response,
                operationName: "file info"
            );

            foreach (var fileId in fileIds)
            {
                var isOnline =
                    fileInfosPerFileId.TryGetValue(fileId, out var fileInfo)
                    && fileInfo.Status == SuccessStatus;

                foreach (var fileUrl in fileUrlsPerFileId[fileId])
                {
                    isOnlinePerFileUrl[fileUrl] = isOnline;
                }
            }
        }

        return isOnlinePerFileUrl;
    }

    public async Task EnsureCredentialsAcceptedAsync(
        StreamtapeConfig config,
        CancellationToken cancellationToken
    )
    {
        var response = await api.GetAccountInfoAsync(
            apiLogin: config.ApiLogin,
            apiKey: config.ApiKey,
            cancellationToken: cancellationToken
        );

        EnsureSuccess(response, "account info");
    }

    public static string? ExtractFileId(string fileUrl)
    {
        if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return pathSegments is ["v" or "e", var fileId, ..] ? fileId : null;
    }

    private static T ReadResult<T>(StreamtapeResponse response, string operationName)
    {
        EnsureSuccess(response, operationName);

        return response.Result.Deserialize<T>(JsonSerializerOptions)
            ?? throw new HttpRequestException(
                $"Streamtape {operationName} response contained no result"
            );
    }

    private static void EnsureSuccess(StreamtapeResponse response, string operationName)
    {
        if (response.Status == SuccessStatus)
        {
            return;
        }

        if (response.Status == CredentialsRejectedStatus)
        {
            throw new HosterCredentialsRejectedException(
                $"Streamtape rejected the API login or API key: {response.Msg}"
            );
        }

        throw new HttpRequestException(
            $"Streamtape {operationName} request failed with status {response.Status}: {response.Msg}"
        );
    }
}
