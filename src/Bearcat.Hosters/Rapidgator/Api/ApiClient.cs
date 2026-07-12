using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Hosters.Extensions;
using Bearcat.Hosters.Rapidgator.Api.File;
using Bearcat.Hosters.Rapidgator.Api.Folder;
using Bearcat.Hosters.Rapidgator.Api.User;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Rapidgator.Api;

public class ApiClient(
    IRapidgatorApi api,
    HttpClientProvider httpClientProvider,
    ILogger<ApiClient> logger
) : IRapidgatorApiClient
{
    private readonly KeyedAuthTokenCache authTokenCache = new(TimeSpan.FromSeconds(400));

    public async Task<UploadFileResponse> RequestUploadFileAsync(
        string name,
        long size,
        string hash,
        string? folderId,
        RapidgatorConfig config,
        CancellationToken cancellationToken
    )
    {
        var token = await GetAuthTokenAsync(config, cancellationToken);
        return await api.RequestUploadFileAsync(
            token: token,
            name: name,
            size: size,
            hash: hash,
            folderId: folderId,
            cancellationToken: cancellationToken
        );
    }

    public async Task<UploadFileResponse> ChangeFileModeAsync(
        RapidgatorConfig config,
        string fileId,
        UploadMode mode,
        CancellationToken cancellationToken
    )
    {
        var token = await GetAuthTokenAsync(config, cancellationToken);
        return await api.ChangeFileModeAsync(
            token: token,
            fileId: fileId,
            mode: (int)mode,
            cancellationToken: cancellationToken
        );
    }

    public async Task<string> CreateFolderAsync(
        string folderName,
        RapidgatorConfig config,
        CancellationToken cancellationToken
    )
    {
        var token = await GetAuthTokenAsync(config, cancellationToken);
        var rootFolder = await api.GetFolderInfoAsync(
            token: token,
            folderId: null,
            cancellationToken: cancellationToken
        );

        EnsureFolderResponseSucceeded(rootFolder, "Rapidgator root folder lookup failed");

        var existingFolder = rootFolder
            .Response!.Folder!.Folders.Where(folder =>
                string.Equals(folder.Name, folderName, StringComparison.Ordinal)
            )
            .OrderBy(folder => folder.Created)
            .ThenBy(folder => folder.FolderId, StringComparer.Ordinal)
            .FirstOrDefault();

        if (existingFolder is not null)
        {
            return existingFolder.FolderId;
        }

        var createdFolder = await api.CreateFolderAsync(
            token: token,
            name: folderName,
            folderId: rootFolder.Response.Folder.FolderId,
            cancellationToken: cancellationToken
        );

        EnsureFolderResponseSucceeded(createdFolder, "Rapidgator folder creation failed");

        return createdFolder.Response!.Folder!.FolderId;
    }

    public async Task MoveFileToFolderAsync(
        RapidgatorConfig config,
        string fileUrl,
        string folderId,
        CancellationToken cancellationToken
    )
    {
        var fileId = ExtractFileId(fileUrl);

        if (string.IsNullOrWhiteSpace(fileId))
        {
            throw new HttpRequestException(
                $"Could not extract Rapidgator file id from URL {fileUrl}"
            );
        }

        var token = await GetAuthTokenAsync(config, cancellationToken);

        var response = await api.MoveFileAsync(token, fileId, folderId, cancellationToken);
        var result = response.Response?.Result;

        if (
            !((HttpStatusCode)response.Status).IsSuccessStatusCode
            || result is null
            || result.Fail > 0
            || result.Success < 1
        )
        {
            throw new HttpRequestException(
                result?.Errors.Count > 0
                    ? $"Rapidgator file move failed: {string.Join(", ", result.Errors)}"
                    : response.Details
                        ?? $"Rapidgator file move failed with status {response.Status}"
            );
        }
    }

    private static string? ExtractFileId(string fileUrl)
    {
        if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var segments = uri
            .Segments.Select(segment => segment.Trim('/'))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();

        var fileSegmentIndex = Array.FindIndex(
            segments,
            segment => string.Equals(segment, "file", StringComparison.OrdinalIgnoreCase)
        );

        return fileSegmentIndex >= 0 && fileSegmentIndex + 1 < segments.Length
            ? segments[fileSegmentIndex + 1]
            : null;
    }

    public async Task<UploadFileResponse> UploadFileAsync(
        string uploadUrl,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken
    )
    {
        using var httpClient = httpClientProvider.GetUploadClient();
        var httpResponse = await httpClient.PostAsync(
            uploadUrl,
            new MultipartFormDataContent { { new StreamContent(stream), "file", fileName } },
            cancellationToken
        );

        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Upload request failed with status code {httpResponse.StatusCode} for file {fileName}"
            );
        }

        var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        var response = JsonSerializer.Deserialize<UploadFileResponse>(
            content,
            options: new JsonSerializerOptions
            {
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                PropertyNameCaseInsensitive = true,
            }
        )!;

        if (!((HttpStatusCode)response.Status).IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Rapidgator upload endpoint returned API status {Status} for file {FileName}: {Details}. Verifying the final upload status",
                response.Status,
                fileName,
                response.Details
            );
        }

        return response;
    }

    public async Task<UploadFileResponse> GetUploadInfoAsync(
        RapidgatorConfig config,
        string uploadId,
        CancellationToken cancellationToken
    )
    {
        var token = await GetAuthTokenAsync(config, cancellationToken);
        var response = await api.GetFileStatusAsync(token, uploadId, cancellationToken);
        return response;
    }

    public async Task<IReadOnlyDictionary<string, LinkCheckStatus>> CheckLinksAsync(
        RapidgatorConfig config,
        IReadOnlyList<FileUrlToCheckDto> files,
        CancellationToken cancellationToken
    )
    {
        var token = await GetAuthTokenAsync(config, cancellationToken);

        var responses = new List<CheckLinksResponse>();

        foreach (var linksBatch in files.Select(file => file.Url).Chunk(25))
        {
            var response = await api.CheckLinkAsync(
                token: token,
                links: string.Join(',', linksBatch),
                cancellationToken: cancellationToken
            );

            responses.Add(response.Content!);
        }

        var statusPerUrl = responses
            .Where(r => r.Responses is not null)
            .SelectMany(r => r.Responses!)
            .ToDictionary(r => r.Url, r => r.Status == "ACCESS");

        var folderIds = files
            .Where(file => statusPerUrl.GetValueOrDefault(file.Url))
            .Select(file => file.HosterFolderId)
            .Where(folderId => !string.IsNullOrWhiteSpace(folderId))
            .Distinct()
            .ToList();

        var downloadCountByFileId = await GetDownloadCountsByFileIdAsync(
            token: token,
            folderIds: folderIds,
            cancellationToken: cancellationToken
        );

        return files
            .Where(file => statusPerUrl.ContainsKey(file.Url))
            .ToDictionary(
                file => file.Url,
                file =>
                {
                    var fileId = ExtractFileId(file.Url);

                    return new LinkCheckStatus(
                        statusPerUrl[file.Url],
                        fileId is not null ? downloadCountByFileId.GetValueOrDefault(fileId) : null
                    );
                }
            );
    }

    private async Task<Dictionary<string, int?>> GetDownloadCountsByFileIdAsync(
        string token,
        IReadOnlyList<string?> folderIds,
        CancellationToken cancellationToken
    )
    {
        var downloadCountByFileId = new Dictionary<string, int?>();

        foreach (var folderId in folderIds)
        {
            try
            {
                var page = 1;

                while (true)
                {
                    var response = await api.GetFolderContentAsync(
                        token: token,
                        folderId: folderId,
                        page: page,
                        cancellationToken: cancellationToken
                    );

                    var contentFiles = response.Content?.Response?.Folder?.Files ?? [];

                    foreach (var file in contentFiles.Where(file => file.FileId is not null))
                    {
                        downloadCountByFileId[file.FileId] = file.NbDownloads;
                    }

                    var pager = response.Content?.Response?.Pager;

                    if (pager is null || pager.Current >= pager.Total)
                    {
                        break;
                    }

                    page++;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to fetch Rapidgator folder content for folder {FolderId} to read download counts: {Message}",
                    folderId,
                    ex.InnerException?.Message ?? ex.Message
                );
            }
        }

        return downloadCountByFileId;
    }

    private async Task<string> GetAuthTokenAsync(
        RapidgatorConfig config,
        CancellationToken cancellationToken
    )
    {
        return await authTokenCache.GetOrAuthenticateAsync(
            config.Username,
            async ct =>
            {
                logger.LogInformation(
                    "Authenticating to Rapidgator for user {Username}",
                    config.Username
                );
                var loginResponse = await LoginAsync(
                    login: config.Username,
                    password: config.Password,
                    cancellationToken: ct
                );

                return loginResponse.Response.Token;
            },
            cancellationToken
        );
    }

    private static void EnsureFolderResponseSucceeded(FolderResponse response, string message)
    {
        if (
            response.Status != (int)HttpStatusCode.OK
            || string.IsNullOrWhiteSpace(response.Response?.Folder?.FolderId)
        )
        {
            throw new HttpRequestException($"{message}: {response.Details}");
        }
    }

    private async Task<LoginResponse> LoginAsync(
        string login,
        string password,
        CancellationToken cancellationToken
    )
    {
        var response = await api.LoginAsync(login, password, cancellationToken);
        return response.Content!;
    }
}
