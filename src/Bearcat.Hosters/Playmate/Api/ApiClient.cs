using System.Globalization;
using System.Text.Json;
using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Shared.XVideoSharing.Api;
using Microsoft.Extensions.Logging;
using Refit;

namespace Bearcat.Hosters.Playmate.Api;

public class ApiClient(
    IPlaymateApi api,
    HttpClientProvider httpClientProvider,
    ILogger<ApiClient> logger
) : XVideoSharingApiClient(httpClientProvider, logger)
{
    public const string ApiBaseUrl = "https://api.playmate.to";

    private const int FileNotFoundStatus = 404;

    private const string DeletedState = "deleted";

    protected override string HosterName => "Playmate";

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
        var uploadServer = DeserializeResult<UploadServerResult>(result);

        return new XVideoSharingUploadServer(
            UploadUrl: uploadServer.UploadUrl,
            ApiKeyFieldName: uploadServer.KeyFieldName,
            FileFieldName: uploadServer.FileFieldName
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
                $"Playmate upload response contained no file: {responseContent}"
            );
        }

        var fileCode = new Uri(uploadedFile.EmbedUrl)
            .AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Last();

        return new XVideoSharingUploadedFile(FileCode: fileCode, EmbedUrl: uploadedFile.EmbedUrl);
    }

    protected override IReadOnlyList<XVideoSharingFileStatus> ReadFileStatuses(JsonElement result)
    {
        return DeserializeResult<List<FileInfoResult>>(result)
            .Select(fileInfo => new XVideoSharingFileStatus(
                FileCode: fileInfo.FileCode,
                IsOnline: fileInfo.Status != FileNotFoundStatus
                    && !string.Equals(
                        fileInfo.State,
                        DeletedState,
                        StringComparison.OrdinalIgnoreCase
                    )
            ))
            .ToList();
    }

    protected override IReadOnlyList<XVideoSharingFolder> ReadFolders(JsonElement result)
    {
        return DeserializeResult<List<ListedFolderResult>>(result)
            .Select(folder => new XVideoSharingFolder(
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
}
