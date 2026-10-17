using System.Text.Json;
using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Shared.XVideoSharing.Api;
using Microsoft.Extensions.Logging;
using Refit;

namespace Bearcat.Hosters.Videro.Api;

public class ApiClient(
    IVideroApi api,
    HttpClientProvider httpClientProvider,
    ILogger<ApiClient> logger
) : XVideoSharingApiClient(httpClientProvider, logger)
{
    public const string ApiBaseUrl = "https://videro.im/api/v1";

    private const string FileBaseUrl = "https://videro.my/d/";

    private const string EmbedBaseUrl = "https://videro.my/e/";

    private const string ApiKeyFieldName = "key";

    private const string FileFieldName = "file";

    private const int FileNotFoundStatus = 404;

    protected override string HosterName => "Videro";

    protected override int MaxFileCodesPerFileInfoRequest => 1;

    protected override TimeSpan FileInfoRequestInterval => TimeSpan.FromMilliseconds(250);

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
                $"Videro upload response contained no file: {responseContent}"
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
                IsOnline: true
            ))
            .ToList();
    }

    protected override bool IsUnknownFileCodesResponse(XVideoSharingResponse response) =>
        response.Status == FileNotFoundStatus;
}
