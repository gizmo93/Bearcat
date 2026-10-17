using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster.Exceptions;
using Microsoft.Extensions.Logging;
using Refit;

namespace Bearcat.Hosters.Shared.XVideoSharing.Api;

public abstract class XVideoSharingApiClient(HttpClientProvider httpClientProvider, ILogger logger)
{
    private const string MimeType = "application/octet-stream";

    protected const int SuccessStatus = 200;

    protected static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
    };

    protected abstract string HosterName { get; }

    protected virtual int MaxFileCodesPerFileInfoRequest => 50;

    protected virtual TimeSpan FileInfoRequestInterval => TimeSpan.Zero;

    public async Task EnsureCredentialsAcceptedAsync(
        string apiKey,
        CancellationToken cancellationToken
    )
    {
        await ReadSuccessfulResultAsync(
            RequestAccountInfoAsync(apiKey, cancellationToken),
            "account info"
        );
    }

    public async Task<XVideoSharingUploadedFile> UploadFileAsync(
        string apiKey,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken
    )
    {
        var uploadServerResult = await ReadSuccessfulResultAsync(
            RequestUploadServerAsync(apiKey, cancellationToken),
            "upload server"
        );
        var uploadServer = ReadUploadServer(uploadServerResult);

        using var httpClient = httpClientProvider.GetUploadClient();
        using var multipartContent = new MultipartFormDataContent();

        multipartContent.Add(new StringContent(apiKey), uploadServer.ApiKeyFieldName);

        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(MimeType);
        multipartContent.Add(fileContent, uploadServer.FileFieldName, fileName);

        using var httpResponse = await httpClient.PostAsync(
            BuildUploadUrl(uploadServer.UploadUrl, apiKey),
            multipartContent,
            cancellationToken
        );

        var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new HosterCredentialsRejectedException(
                $"{HosterName} rejected the API key: {content}"
            );
        }

        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"{HosterName} upload request failed with status code {httpResponse.StatusCode}: {content}",
                inner: null,
                statusCode: httpResponse.StatusCode
            );
        }

        return ReadUploadedFile(content);
    }

    public async Task<Dictionary<string, bool>> GetOnlineStatusAsync(
        string apiKey,
        IReadOnlySet<string> fileCodes,
        CancellationToken cancellationToken
    )
    {
        var isOnlinePerFileCode = fileCodes.ToDictionary(
            fileCode => fileCode,
            _ => false,
            StringComparer.Ordinal
        );

        foreach (
            var (chunkIndex, fileCodeChunk) in fileCodes
                .Chunk(MaxFileCodesPerFileInfoRequest)
                .Index()
        )
        {
            if (chunkIndex > 0)
            {
                await Task.Delay(FileInfoRequestInterval, cancellationToken);
            }

            var fileInfoResponse = await ReadResponseAsync(
                RequestFileInfoAsync(apiKey, string.Join(',', fileCodeChunk), cancellationToken)
            );

            if (IsUnknownFileCodesResponse(fileInfoResponse))
            {
                continue;
            }

            EnsureSuccess(fileInfoResponse, "file info");

            foreach (var fileStatus in ReadFileStatuses(fileInfoResponse.Result))
            {
                isOnlinePerFileCode[fileStatus.FileCode] = fileStatus.IsOnline;
            }
        }

        return isOnlinePerFileCode;
    }

    public async Task<string> CreateFolderAsync(
        string apiKey,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        var folderListResult = await ReadSuccessfulResultAsync(
            RequestFolderListAsync(apiKey, cancellationToken),
            "folder list"
        );
        var existingFolder = ReadFolders(folderListResult)
            .FirstOrDefault(folder =>
                string.Equals(folder.Name, folderName, StringComparison.OrdinalIgnoreCase)
            );

        if (existingFolder is not null)
        {
            logger.LogInformation(
                "Reusing existing {HosterName} folder {FolderName} with id {FolderId}",
                HosterName,
                folderName,
                existingFolder.FolderId
            );

            return existingFolder.FolderId;
        }

        logger.LogInformation("Creating {HosterName} folder {FolderName}", HosterName, folderName);

        var folderCreationResult = await ReadSuccessfulResultAsync(
            RequestFolderCreationAsync(apiKey, folderName, cancellationToken),
            "folder creation"
        );

        return ReadCreatedFolderId(folderCreationResult);
    }

    public async Task MoveFileToFolderAsync(
        string apiKey,
        string fileCode,
        string folderId,
        CancellationToken cancellationToken
    )
    {
        await ReadSuccessfulResultAsync(
            RequestFileMoveAsync(apiKey, fileCode, folderId, cancellationToken),
            "file move"
        );
    }

    protected abstract Task<ApiResponse<XVideoSharingResponse>> RequestAccountInfoAsync(
        string apiKey,
        CancellationToken cancellationToken
    );

    protected abstract Task<ApiResponse<XVideoSharingResponse>> RequestUploadServerAsync(
        string apiKey,
        CancellationToken cancellationToken
    );

    protected abstract Task<ApiResponse<XVideoSharingResponse>> RequestFileInfoAsync(
        string apiKey,
        string fileCodes,
        CancellationToken cancellationToken
    );

    protected virtual Task<ApiResponse<XVideoSharingResponse>> RequestFolderListAsync(
        string apiKey,
        CancellationToken cancellationToken
    )
    {
        throw CreateFoldersNotSupportedException();
    }

    protected virtual Task<ApiResponse<XVideoSharingResponse>> RequestFolderCreationAsync(
        string apiKey,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        throw CreateFoldersNotSupportedException();
    }

    protected virtual Task<ApiResponse<XVideoSharingResponse>> RequestFileMoveAsync(
        string apiKey,
        string fileCode,
        string folderId,
        CancellationToken cancellationToken
    )
    {
        throw CreateFoldersNotSupportedException();
    }

    protected abstract XVideoSharingUploadServer ReadUploadServer(JsonElement result);

    protected abstract XVideoSharingUploadedFile ReadUploadedFile(string responseContent);

    protected abstract IReadOnlyList<XVideoSharingFileStatus> ReadFileStatuses(JsonElement result);

    protected virtual IReadOnlyList<XVideoSharingFolder> ReadFolders(JsonElement result)
    {
        throw CreateFoldersNotSupportedException();
    }

    protected virtual string ReadCreatedFolderId(JsonElement result)
    {
        throw CreateFoldersNotSupportedException();
    }

    protected virtual string BuildUploadUrl(string uploadServerUrl, string apiKey) =>
        uploadServerUrl;

    protected virtual bool IsApiKeyRejected(XVideoSharingResponse response) =>
        response.Status is 401 or 403;

    protected virtual bool IsUnknownFileCodesResponse(XVideoSharingResponse response) => false;

    protected T DeserializeResult<T>(JsonElement result)
    {
        return result.Deserialize<T>(JsonSerializerOptions)
            ?? throw new HttpRequestException($"{HosterName} response contained no result");
    }

    private async Task<JsonElement> ReadSuccessfulResultAsync(
        Task<ApiResponse<XVideoSharingResponse>> request,
        string operationName
    )
    {
        var response = await ReadResponseAsync(request);

        EnsureSuccess(response, operationName);

        return response.Result;
    }

    private async Task<XVideoSharingResponse> ReadResponseAsync(
        Task<ApiResponse<XVideoSharingResponse>> request
    )
    {
        using var apiResponse = await request;

        return ReadResponse(apiResponse);
    }

    private XVideoSharingResponse ReadResponse(ApiResponse<XVideoSharingResponse> apiResponse)
    {
        if (apiResponse.Content is not null)
        {
            return apiResponse.Content;
        }

        if (apiResponse.Error is null)
        {
            throw new HttpRequestException($"{HosterName} response contained no content");
        }

        if (apiResponse.Error is not ApiException apiException)
        {
            throw apiResponse.Error;
        }

        var errorResponse = TryDeserializeResponse(apiException.Content);

        if (errorResponse is not null)
        {
            return errorResponse;
        }

        if (apiResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"{HosterName} returned an unreadable response: {apiException.Content}"
            );
        }

        return new XVideoSharingResponse(
            (int)apiException.StatusCode,
            apiException.Content?.Trim(),
            default
        );
    }

    private static XVideoSharingResponse? TryDeserializeResponse(string? rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<XVideoSharingResponse>(
                rawContent,
                JsonSerializerOptions
            );
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void EnsureSuccess(XVideoSharingResponse response, string operationName)
    {
        if (response.Status == SuccessStatus)
        {
            return;
        }

        if (IsApiKeyRejected(response))
        {
            throw new HosterCredentialsRejectedException(
                $"{HosterName} rejected the API key: {response.Msg}"
            );
        }

        throw new HttpRequestException(
            $"{HosterName} {operationName} request failed with status {response.Status}: {response.Msg}"
        );
    }

    private NotSupportedException CreateFoldersNotSupportedException()
    {
        return new NotSupportedException($"{HosterName} does not support folders");
    }
}
