using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.MixDrop.Api;

public class ApiClient(
    IMixDropApi api,
    HttpClientProvider httpClientProvider,
    ILogger<ApiClient> logger
)
{
    public const string ApiBaseUrl = "https://api.mixdrop.ag";

    public const string UploadUrl = "https://ul.mixdrop.ag/api";

    private const string MimeType = "application/octet-stream";

    private const int MaxFileReferencesPerFileInfoRequest = 50;

    private const string InvalidLoginMessage = "Invalid login";

    private const string FileNotFoundStatus = "notfound";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<UploadedFileResult> UploadFileAsync(
        MixDropConfig config,
        Stream stream,
        string fileName,
        string? folderId,
        CancellationToken cancellationToken
    )
    {
        using var httpClient = httpClientProvider.GetUploadClient();
        using var multipartContent = new MultipartFormDataContent();

        multipartContent.Add(new StringContent(config.EmailAddress), "email");
        multipartContent.Add(new StringContent(config.ApiKey), "key");

        if (!string.IsNullOrWhiteSpace(folderId))
        {
            multipartContent.Add(new StringContent(folderId), "folder");
        }

        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(MimeType);
        multipartContent.Add(fileContent, "file", fileName);

        using var httpResponse = await httpClient.PostAsync(
            UploadUrl,
            multipartContent,
            cancellationToken
        );

        var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"MixDrop upload request failed with status code {httpResponse.StatusCode}: {content}",
                inner: null,
                statusCode: httpResponse.StatusCode
            );
        }

        var response =
            JsonSerializer.Deserialize<MixDropResponse>(content, JsonSerializerOptions)
            ?? throw new HttpRequestException("MixDrop upload response was empty");

        return ReadResult<UploadedFileResult>(response, "upload");
    }

    public async Task<string> CreateFolderAsync(
        MixDropConfig config,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        var existingFolderId = await GetExistingFolderIdAsync(
            config,
            folderName,
            cancellationToken
        );

        if (existingFolderId is not null)
        {
            logger.LogInformation(
                "Reusing existing MixDrop folder {FolderName} with id {FolderId}",
                folderName,
                existingFolderId
            );

            return existingFolderId.Value.ToString(CultureInfo.InvariantCulture);
        }

        logger.LogInformation("Creating MixDrop folder {FolderName}", folderName);

        var response = await api.CreateFolderAsync(
            emailAddress: config.EmailAddress,
            apiKey: config.ApiKey,
            title: folderName,
            cancellationToken: cancellationToken
        );

        var createdFolder = ReadResult<CreatedFolderResult>(response, "folder creation");

        return createdFolder.FolderId.ToString(CultureInfo.InvariantCulture);
    }

    public async Task MoveFileToFolderAsync(
        MixDropConfig config,
        string fileUrl,
        string? externalId,
        string folderId,
        CancellationToken cancellationToken
    )
    {
        var fileReference =
            externalId
            ?? ExtractFileReference(fileUrl)
            ?? throw new HttpRequestException(
                $"Could not extract the MixDrop file reference from URL {fileUrl}"
            );

        var response = await api.MoveFilesAsync(
            emailAddress: config.EmailAddress,
            apiKey: config.ApiKey,
            fileReferences: [fileReference],
            folderId: folderId,
            cancellationToken: cancellationToken
        );

        EnsureSuccess(response, "file move");
    }

    public async Task<IReadOnlyDictionary<string, bool>> CheckLinksAsync(
        MixDropConfig config,
        IReadOnlyList<FileUrlToCheckDto> files,
        CancellationToken cancellationToken
    )
    {
        var fileUrlsPerFileReference = files
            .DistinctBy(file => file.Url)
            .Select(file => new
            {
                FileReference = file.ExternalId ?? ExtractFileReference(file.Url),
                file.Url,
            })
            .Where(entry => entry.FileReference is not null)
            .GroupBy(entry => entry.FileReference!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(entry => entry.Url).ToList(),
                StringComparer.Ordinal
            );

        var isOnlinePerFileUrl = new Dictionary<string, bool>();

        foreach (
            var fileReferences in fileUrlsPerFileReference.Keys.Chunk(
                MaxFileReferencesPerFileInfoRequest
            )
        )
        {
            var response = await api.GetFileInfoAsync(
                emailAddress: config.EmailAddress,
                apiKey: config.ApiKey,
                fileReferences: fileReferences,
                cancellationToken: cancellationToken
            );

            var fileInfosPerFileReference = ReadResult<Dictionary<string, FileInfoResult>>(
                response: response,
                operationName: "file info"
            );

            foreach (var (fileReference, fileInfo) in fileInfosPerFileReference)
            {
                if (!fileUrlsPerFileReference.TryGetValue(fileReference, out var fileUrls))
                {
                    continue;
                }

                var isOnline = !fileInfo.IsDeleted && fileInfo.Status != FileNotFoundStatus;

                foreach (var fileUrl in fileUrls)
                {
                    isOnlinePerFileUrl[fileUrl] = isOnline;
                }
            }
        }

        return isOnlinePerFileUrl;
    }

    public async Task EnsureCredentialsAcceptedAsync(
        MixDropConfig config,
        CancellationToken cancellationToken
    )
    {
        var response = await api.GetFolderListAsync(
            emailAddress: config.EmailAddress,
            apiKey: config.ApiKey,
            page: null,
            cancellationToken: cancellationToken
        );

        EnsureSuccess(response, "folder list");
    }

    public static string? ExtractFileReference(string fileUrl)
    {
        if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return pathSegments is ["f" or "e", var fileReference, ..] ? fileReference : null;
    }

    private async Task<long?> GetExistingFolderIdAsync(
        MixDropConfig config,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        var page = 1;
        var pageCount = 1;

        while (page <= pageCount)
        {
            var response = await api.GetFolderListAsync(
                emailAddress: config.EmailAddress,
                apiKey: config.ApiKey,
                page: page,
                cancellationToken: cancellationToken
            );

            EnsureSuccess(response, "folder list");

            var existingFolder = ReadListedFolders(response.Result)
                .FirstOrDefault(folder =>
                    string.Equals(folder.Title, folderName, StringComparison.OrdinalIgnoreCase)
                );

            if (existingFolder is not null)
            {
                return existingFolder.FolderId;
            }

            pageCount = response.Pages ?? pageCount;
            page++;
        }

        return null;
    }

    private static List<ListedFolderResult> ReadListedFolders(JsonElement result)
    {
        if (
            result.ValueKind != JsonValueKind.Object
            || !result.TryGetProperty("folders", out var folders)
        )
        {
            return [];
        }

        return folders.Deserialize<List<ListedFolderResult>>(JsonSerializerOptions) ?? [];
    }

    private static T ReadResult<T>(MixDropResponse response, string operationName)
    {
        EnsureSuccess(response, operationName);

        return response.Result.Deserialize<T>(JsonSerializerOptions)
            ?? throw new HttpRequestException(
                $"MixDrop {operationName} response contained no result"
            );
    }

    private static void EnsureSuccess(MixDropResponse response, string operationName)
    {
        if (response.Success)
        {
            return;
        }

        var errorMessage = GetErrorMessage(response.Result);

        if (string.Equals(errorMessage, InvalidLoginMessage, StringComparison.OrdinalIgnoreCase))
        {
            throw new HosterCredentialsRejectedException(
                $"MixDrop rejected the email address or API key: {errorMessage}"
            );
        }

        throw new HttpRequestException($"MixDrop {operationName} request failed: {errorMessage}");
    }

    private static string GetErrorMessage(JsonElement result)
    {
        return
            result.ValueKind == JsonValueKind.Object
            && result.TryGetProperty("msg", out var message)
            && message.ValueKind == JsonValueKind.String
            ? message.GetString()!
            : result.GetRawText();
    }
}
