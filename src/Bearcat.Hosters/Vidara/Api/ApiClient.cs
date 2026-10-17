using System.Globalization;
using System.Text.Json;
using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Shared.XVideoSharing.Api;
using Microsoft.Extensions.Logging;
using Refit;

namespace Bearcat.Hosters.Vidara.Api;

public class ApiClient(
    IVidaraApi api,
    HttpClientProvider httpClientProvider,
    ILogger<ApiClient> logger
) : XVideoSharingApiClient(httpClientProvider, logger)
{
    public const string ApiBaseUrl = "https://api.vidara.so/v1";

    private const string FileBaseUrl = "https://vidara.to/";

    private const string ApiKeyFieldName = "api_key";

    private const string FileFieldName = "file";

    private const int ApiKeyRejectedStatus = 401;

    private const int UnknownFileCodeStatus = 403;

    private static readonly IReadOnlySet<string> OfflineStates = new HashSet<string>(
        ["blocked", "error", "deleted"],
        StringComparer.OrdinalIgnoreCase
    );

    protected override string HosterName => "Vidara";

    protected override int MaxFileCodesPerFileInfoRequest => 1;

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
        return api.GetFolderListAsync(apiKey, cancellationToken);
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

    protected override XVideoSharingUploadServer ReadUploadServer(JsonElement result)
    {
        return new XVideoSharingUploadServer(
            UploadUrl: DeserializeResult<UploadServerResult>(result).UploadUrl,
            ApiKeyFieldName: ApiKeyFieldName,
            FileFieldName: FileFieldName
        );
    }

    protected override XVideoSharingUploadedFile ReadUploadedFile(string responseContent)
    {
        var uploadedFile = JsonSerializer.Deserialize<UploadedFileResult>(
            responseContent,
            JsonSerializerOptions
        );

        if (uploadedFile?.EmbedUrl is null)
        {
            throw new HttpRequestException(
                $"Vidara upload response contained no file: {responseContent}"
            );
        }

        var fileCode = new Uri(uploadedFile.EmbedUrl)
            .AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Last();

        return new XVideoSharingUploadedFile(
            FileCode: fileCode,
            FileUrl: FileBaseUrl + fileCode,
            EmbedUrl: uploadedFile.EmbedUrl
        );
    }

    protected override IReadOnlyList<XVideoSharingFileStatus> ReadFileStatuses(JsonElement result)
    {
        return DeserializeResult<List<FileInfoResult>>(result)
            .DistinctBy(fileInfo => fileInfo.FileCode, StringComparer.Ordinal)
            .Select(fileInfo => new XVideoSharingFileStatus(
                FileCode: fileInfo.FileCode,
                IsOnline: !OfflineStates.Contains(fileInfo.Status)
            ))
            .ToList();
    }

    protected override IReadOnlyList<XVideoSharingFolder> ReadFolders(JsonElement result)
    {
        return DeserializeResult<FolderListResult>(result)
            .Folders.Select(folder => new XVideoSharingFolder(
                FolderId: folder.FolderId.ToString(CultureInfo.InvariantCulture),
                Name: folder.Name
            ))
            .ToList();
    }

    protected override string ReadCreatedFolderId(JsonElement result)
    {
        return DeserializeResult<CreatedFolderResult>(result)
            .FolderId.ToString(CultureInfo.InvariantCulture);
    }

    protected override bool IsApiKeyRejected(XVideoSharingResponse response) =>
        response.Status == ApiKeyRejectedStatus;

    protected override bool IsUnknownFileCodesResponse(XVideoSharingResponse response) =>
        response.Status == UnknownFileCodeStatus;
}
