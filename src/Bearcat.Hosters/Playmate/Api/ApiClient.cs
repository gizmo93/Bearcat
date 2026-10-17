using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Playmate.Api;

public class ApiClient(
    IPlaymateApi api,
    HttpClientProvider httpClientProvider,
    ILogger<ApiClient> logger
)
{
    public const string ApiBaseUrl = "https://api.playmate.to";

    private const string MimeType = "application/octet-stream";

    private const int MaxFileCodesPerFileInfoRequest = 50;

    private const int SuccessStatus = 200;

    private const int InvalidApiKeyStatus = 401;

    private const int FileNotFoundStatus = 404;

    private const string DeletedState = "deleted";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<UploadedFileResult> UploadFileAsync(
        PlaymateConfig config,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken
    )
    {
        var uploadServerResponse = await api.GetUploadServerAsync(config.ApiKey, cancellationToken);
        var uploadServer = ReadResult<UploadServerResult>(uploadServerResponse, "upload server");

        using var httpClient = httpClientProvider.GetUploadClient();
        using var multipartContent = new MultipartFormDataContent();

        multipartContent.Add(new StringContent(config.ApiKey), uploadServer.KeyFieldName);

        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(MimeType);
        multipartContent.Add(fileContent, uploadServer.FileFieldName, fileName);

        using var httpResponse = await httpClient.PostAsync(
            uploadServer.UploadUrl,
            multipartContent,
            cancellationToken
        );

        var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Playmate upload request failed with status code {httpResponse.StatusCode}: {content}",
                inner: null,
                statusCode: httpResponse.StatusCode
            );
        }

        var uploadedFile = JsonSerializer.Deserialize<UploadedFileResult>(
            content,
            JsonSerializerOptions
        );

        if (uploadedFile?.EmbedUrl is null)
        {
            throw new HttpRequestException(
                $"Playmate upload response contained no file: {content}"
            );
        }

        return uploadedFile;
    }

    public async Task<string> CreateFolderAsync(
        PlaymateConfig config,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        var folderListResponse = await api.GetFolderListAsync(config.ApiKey, cancellationToken);
        var existingFolder = ReadResult<List<ListedFolderResult>>(folderListResponse, "folder list")
            .FirstOrDefault(folder =>
                string.Equals(folder.Name, folderName, StringComparison.OrdinalIgnoreCase)
            );

        if (existingFolder is not null)
        {
            logger.LogInformation(
                "Reusing existing Playmate folder {FolderName} with id {FolderId}",
                folderName,
                existingFolder.FolderId
            );

            return existingFolder.FolderId.ToString(CultureInfo.InvariantCulture);
        }

        logger.LogInformation("Creating Playmate folder {FolderName}", folderName);

        var response = await api.CreateFolderAsync(
            apiKey: config.ApiKey,
            name: folderName,
            cancellationToken: cancellationToken
        );

        var createdFolder = ReadResult<CreatedFolderResult>(response, "folder creation");

        return createdFolder.FolderId.ToString(CultureInfo.InvariantCulture);
    }

    public async Task MoveFileToFolderAsync(
        PlaymateConfig config,
        string fileUrl,
        string? externalId,
        string folderId,
        CancellationToken cancellationToken
    )
    {
        var fileCode =
            externalId
            ?? ExtractFileCode(fileUrl)
            ?? throw new HttpRequestException(
                $"Could not extract the Playmate file code from URL {fileUrl}"
            );

        var response = await api.MoveFileToFolderAsync(
            apiKey: config.ApiKey,
            fileCode: fileCode,
            folderId: folderId,
            cancellationToken: cancellationToken
        );

        EnsureSuccess(response, "file move");
    }

    public async Task<IReadOnlyDictionary<string, bool>> CheckLinksAsync(
        PlaymateConfig config,
        IReadOnlyList<FileUrlToCheckDto> files,
        CancellationToken cancellationToken
    )
    {
        var fileUrlsPerFileCode = files
            .DistinctBy(file => file.Url)
            .Select(file => new
            {
                FileCode = file.ExternalId ?? ExtractFileCode(file.Url),
                file.Url,
            })
            .Where(entry => entry.FileCode is not null)
            .GroupBy(entry => entry.FileCode!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(entry => entry.Url).ToList(),
                StringComparer.Ordinal
            );

        var isOnlinePerFileUrl = new Dictionary<string, bool>();

        foreach (var fileCodes in fileUrlsPerFileCode.Keys.Chunk(MaxFileCodesPerFileInfoRequest))
        {
            var response = await api.GetFileInfoAsync(
                apiKey: config.ApiKey,
                fileCodes: fileCodes,
                cancellationToken: cancellationToken
            );

            var fileInfos = ReadResult<List<FileInfoResult>>(response, "file info");

            foreach (var fileInfo in fileInfos)
            {
                if (!fileUrlsPerFileCode.TryGetValue(fileInfo.FileCode, out var fileUrls))
                {
                    continue;
                }

                var isOnline =
                    fileInfo.Status != FileNotFoundStatus
                    && !string.Equals(
                        fileInfo.State,
                        DeletedState,
                        StringComparison.OrdinalIgnoreCase
                    );

                foreach (var fileUrl in fileUrls)
                {
                    isOnlinePerFileUrl[fileUrl] = isOnline;
                }
            }
        }

        return isOnlinePerFileUrl;
    }

    public async Task EnsureCredentialsAcceptedAsync(
        PlaymateConfig config,
        CancellationToken cancellationToken
    )
    {
        var response = await api.GetAccountInfoAsync(config.ApiKey, cancellationToken);

        EnsureSuccess(response, "account info");
    }

    public static string? ExtractFileCode(string fileUrl)
    {
        if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return pathSegments is ["embed", var fileCode] ? fileCode : null;
    }

    private static T ReadResult<T>(PlaymateResponse response, string operationName)
    {
        EnsureSuccess(response, operationName);

        return response.Result.Deserialize<T>(JsonSerializerOptions)
            ?? throw new HttpRequestException(
                $"Playmate {operationName} response contained no result"
            );
    }

    private static void EnsureSuccess(PlaymateResponse response, string operationName)
    {
        if (response.Status == SuccessStatus)
        {
            return;
        }

        if (response.Status == InvalidApiKeyStatus)
        {
            throw new HosterCredentialsRejectedException(
                $"Playmate rejected the API key: {response.Message}"
            );
        }

        throw new HttpRequestException(
            $"Playmate {operationName} request failed with status {response.Status}: {response.Message}"
        );
    }
}
