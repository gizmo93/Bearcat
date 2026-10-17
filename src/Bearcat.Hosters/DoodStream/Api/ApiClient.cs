using System.Text.Json;
using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Shared.XVideoSharing.Api;
using Microsoft.Extensions.Logging;
using Refit;

namespace Bearcat.Hosters.DoodStream.Api;

public class ApiClient(
    IDoodStreamApi api,
    HttpClientProvider httpClientProvider,
    ILogger<ApiClient> logger
) : XVideoSharingApiClient(httpClientProvider, logger)
{
    public const string ApiBaseUrl = "https://doodapi.co/api";

    private const string FileBaseUrl = "https://doodstream.com/d/";

    private const string EmbedBaseUrl = "https://doodstream.com/e/";

    private const string RootFolderId = "0";

    private const string ApiKeyFieldName = "api_key";

    private const string FileFieldName = "file";

    private const int InvalidRequestStatus = 400;

    private const string InvalidKeyMessage = "Invalid key";

    private const string InvalidFileCodesMessage = "Invalid file codes";

    protected override string HosterName => "DoodStream";

    protected override Task<ApiResponse<XVideoSharingResponse>> RequestAccountInfoAsync(
        string apiKey,
        CancellationToken cancellationToken
    )
    {
        return api.GetAccountInfoAsync(apiKey, cancellationToken);
    }

    protected override Task<ApiResponse<XVideoSharingResponse>> RequestUploadServerAsync(
        string apiKey,
        CancellationToken cancellationToken
    )
    {
        return api.GetUploadServerAsync(apiKey, cancellationToken);
    }

    protected override Task<ApiResponse<XVideoSharingResponse>> RequestFileInfoAsync(
        string apiKey,
        string fileCodes,
        CancellationToken cancellationToken
    )
    {
        return api.GetFileInfoAsync(apiKey, fileCodes, cancellationToken);
    }

    protected override Task<ApiResponse<XVideoSharingResponse>> RequestFolderListAsync(
        string apiKey,
        CancellationToken cancellationToken
    )
    {
        return api.GetFolderListAsync(apiKey, RootFolderId, cancellationToken);
    }

    protected override Task<ApiResponse<XVideoSharingResponse>> RequestFolderCreationAsync(
        string apiKey,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        return api.CreateFolderAsync(apiKey, folderName, cancellationToken);
    }

    protected override Task<ApiResponse<XVideoSharingResponse>> RequestFileMoveAsync(
        string apiKey,
        string fileCode,
        string folderId,
        CancellationToken cancellationToken
    )
    {
        return api.MoveFileToFolderAsync(apiKey, fileCode, folderId, cancellationToken);
    }

    protected override string BuildUploadUrl(string uploadServerUrl, string apiKey) =>
        $"{uploadServerUrl}?{apiKey}";

    protected override XVideoSharingUploadServer ReadUploadServer(JsonElement result)
    {
        return new XVideoSharingUploadServer(
            UploadUrl: DeserializeResult<string>(result),
            ApiKeyFieldName: ApiKeyFieldName,
            FileFieldName: FileFieldName
        );
    }

    protected override XVideoSharingUploadedFile ReadUploadedFile(string responseContent)
    {
        var response = JsonSerializer.Deserialize<XVideoSharingResponse>(
            responseContent,
            JsonSerializerOptions
        );

        var uploadedFile =
            response?.Status == SuccessStatus
                ? DeserializeResult<List<UploadedFileResult>>(response.Result).FirstOrDefault()
                : null;

        if (uploadedFile is null)
        {
            throw new HttpRequestException(
                $"DoodStream upload response contained no file: {responseContent}"
            );
        }

        return new XVideoSharingUploadedFile(
            FileCode: uploadedFile.FileCode,
            FileUrl: FileBaseUrl + uploadedFile.FileCode,
            EmbedUrl: EmbedBaseUrl + uploadedFile.FileCode
        );
    }

    protected override IReadOnlyList<XVideoSharingFileStatus> ReadFileStatuses(JsonElement result)
    {
        return DeserializeResult<List<FileInfoResult>>(result)
            .Select(fileInfo => new XVideoSharingFileStatus(
                FileCode: fileInfo.FileCode,
                IsOnline: fileInfo.Status == SuccessStatus
            ))
            .ToList();
    }

    protected override IReadOnlyList<XVideoSharingFolder> ReadFolders(JsonElement result)
    {
        return DeserializeResult<FolderListResult>(result)
            .Folders.Select(folder => new XVideoSharingFolder(
                FolderId: folder.FolderId,
                Name: folder.Name
            ))
            .ToList();
    }

    protected override string ReadCreatedFolderId(JsonElement result)
    {
        return DeserializeResult<CreatedFolderResult>(result).FolderId;
    }

    protected override bool IsApiKeyRejected(XVideoSharingResponse response)
    {
        return base.IsApiKeyRejected(response)
            || IsInvalidRequestWithMessage(response, InvalidKeyMessage);
    }

    protected override bool IsUnknownFileCodesResponse(XVideoSharingResponse response)
    {
        return IsInvalidRequestWithMessage(response, InvalidFileCodesMessage);
    }

    private static bool IsInvalidRequestWithMessage(XVideoSharingResponse response, string message)
    {
        return response.Status == InvalidRequestStatus
            && string.Equals(response.Msg, message, StringComparison.OrdinalIgnoreCase);
    }
}
