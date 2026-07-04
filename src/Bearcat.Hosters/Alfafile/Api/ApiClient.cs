using System.Net;
using System.Security.Authentication;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Hosters.Alfafile.Api.File;
using Bearcat.Hosters.Alfafile.Api.User;
using Bearcat.Hosters.Extensions;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Refit;

namespace Bearcat.Hosters.Alfafile.Api;

public class ApiClient(
    IAlfafileApi api,
    HttpClientProvider httpClientProvider,
    ILogger<ApiClient> logger
) : IAlfafileApiClient
{
    public TimeSpan RateLimitRetryDelay { get; init; } = TimeSpan.FromSeconds(5);

    private const int MaxParallelLinkChecks = 10;

    private const int MaxLinkCheckAttempts = 3;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
    };

    private readonly KeyedAuthTokenCache authTokenCache = new(TimeSpan.FromSeconds(400));

    public async Task<UploadFileResponse> RequestUploadFileAsync(
        string name,
        long size,
        string hash,
        string? folderId,
        AlfafileConfig config,
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

    public async Task<string> CreateFolderAsync(
        AlfafileConfig config,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        var token = await GetAuthTokenAsync(config, cancellationToken);

        var createdFolder = await api.CreateFolderAsync(
            token: token,
            name: folderName,
            folderId: null,
            cancellationToken: cancellationToken
        );

        if (createdFolder.Status == (int)HttpStatusCode.Conflict)
        {
            var existingFolderId = await GetFolderIdAsync(token, folderName, cancellationToken);

            return existingFolderId
                ?? throw new HttpRequestException(
                    $"Alfafile folder already exists but was not found in root folder: {folderName}"
                );
        }

        if (
            !((HttpStatusCode)createdFolder.Status).IsSuccessStatusCode
            || string.IsNullOrWhiteSpace(createdFolder.Response?.Folder?.FolderId)
        )
        {
            throw new HttpRequestException(
                createdFolder.Details
                    ?? $"Alfafile folder creation failed with status {createdFolder.Status}"
            );
        }

        return createdFolder.Response.Folder.FolderId;
    }

    public async Task MoveFileToFolderAsync(
        AlfafileConfig config,
        string fileUrl,
        string folderId,
        CancellationToken cancellationToken
    )
    {
        var fileId = GetFileId(fileUrl);

        if (string.IsNullOrWhiteSpace(fileId))
        {
            throw new HttpRequestException(
                $"Could not extract Alfafile file id from URL {fileUrl}"
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
                    ? $"Alfafile file move failed: {string.Join(", ", result.Errors)}"
                    : response.Details ?? $"Alfafile file move failed with status {response.Status}"
            );
        }
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
            JsonSerializerOptions
        )!;

        if (!((HttpStatusCode)response.Status).IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Upload failed for file {fileName} with message: {response.Details}"
            );
        }

        return response;
    }

    public async Task<UploadFileResponse> GetUploadInfoAsync(
        AlfafileConfig config,
        string uploadId,
        CancellationToken cancellationToken
    )
    {
        var token = await GetAuthTokenAsync(config, cancellationToken);
        return await api.GetUploadInfoAsync(token, uploadId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, LinkCheckStatus>> CheckLinksAsync(
        AlfafileConfig config,
        IReadOnlyList<string> fileUrls,
        CancellationToken cancellationToken
    )
    {
        var token = await GetAuthTokenAsync(config, cancellationToken);

        using var semaphore = new SemaphoreSlim(MaxParallelLinkChecks);

        var checkTasks = fileUrls
            .Distinct()
            .Select(fileUrl =>
                CheckLinkAsync(
                    token: token,
                    fileUrl: fileUrl,
                    semaphore: semaphore,
                    cancellationToken: cancellationToken
                )
            )
            .ToList();

        var results = await Task.WhenAll(checkTasks);

        var folderIds = results
            .Where(result => result.File is not null)
            .Select(result => result.File!.FolderId)
            .Distinct()
            .ToList();

        var downloadCountByFileId = await GetDownloadCountsByFileIdAsync(
            token: token,
            folderIds: folderIds,
            cancellationToken: cancellationToken
        );

        return results
            .Where(result => result.IsOnline.HasValue)
            .ToDictionary(
                result => result.FileUrl,
                result => new LinkCheckStatus(
                    result.IsOnline!.Value,
                    result.File is { FileId: not null } file
                        ? downloadCountByFileId.GetValueOrDefault(file.FileId)
                        : null
                )
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

                    var files = response.Content?.Response?.Folder?.Files ?? [];

                    foreach (var file in files.Where(file => file.FileId is not null))
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
                    "Failed to fetch Alfafile folder content for folder {FolderId} to read download counts: {Message}",
                    folderId,
                    ex.InnerException?.Message ?? ex.Message
                );
            }
        }

        return downloadCountByFileId;
    }

    private async Task<(string FileUrl, bool? IsOnline, UploadedFile? File)> CheckLinkAsync(
        string token,
        string fileUrl,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken
    )
    {
        var fileId = GetFileId(fileUrl);

        if (string.IsNullOrWhiteSpace(fileId))
        {
            return (fileUrl, null, null);
        }

        foreach (var attempt in Enumerable.Range(1, MaxLinkCheckAttempts))
        {
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                var response = await api.GetFileInfoAsync(token, fileId, cancellationToken);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    logger.LogInformation(
                        "Rate limited by Alfafile API while checking {FileUrl}, waiting before retrying (Attempt {Attempt})",
                        fileUrl,
                        attempt
                    );
                }
                else
                {
                    var file = response.Content?.Response?.File;
                    var isOnline =
                        response.IsSuccessStatusCode
                        && response.Content is { Status: (int)HttpStatusCode.OK }
                        && file is not null;

                    return (fileUrl, isOnline, isOnline ? file : null);
                }
            }
            catch (ApiException exception)
                when (exception.StatusCode == HttpStatusCode.TooManyRequests)
            {
                logger.LogInformation(
                    exception,
                    "Rate limited by Alfafile API while checking {FileUrl}, waiting before retrying (Attempt {Attempt})",
                    fileUrl,
                    attempt
                );
            }
            catch (HttpRequestException exception)
                when (exception.StatusCode == HttpStatusCode.TooManyRequests)
            {
                logger.LogInformation(
                    exception,
                    "Rate limited by Alfafile API while checking {FileUrl}, waiting before retrying (Attempt {Attempt})",
                    fileUrl,
                    attempt
                );
            }
            finally
            {
                semaphore.Release();
            }

            if (attempt < MaxLinkCheckAttempts)
            {
                await Task.Delay(RateLimitRetryDelay, cancellationToken);
            }
        }

        return (fileUrl, null, null);
    }

    public async Task<InfoResponse> GetUserInfoAsync(
        AlfafileConfig config,
        CancellationToken cancellationToken
    )
    {
        var token = await GetAuthTokenAsync(config, cancellationToken);
        var response = await api.GetUserInfoAsync(token, cancellationToken);

        if (!response.IsSuccessStatusCode || response.Content is null)
        {
            throw new HttpRequestException(
                $"User info request failed with status code {response.StatusCode}"
            );
        }

        return response.Content;
    }

    private async Task<string?> GetFolderIdAsync(
        string token,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        var rootFolder = await api.GetFolderInfoAsync(
            token: token,
            folderId: null,
            cancellationToken: cancellationToken
        );

        if (!((HttpStatusCode)rootFolder.Status).IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                rootFolder.Details ?? $"Alfafile folder info failed with status {rootFolder.Status}"
            );
        }

        return rootFolder
            .Response?.Folder?.Folders.FirstOrDefault(folder =>
                string.Equals(folder.Name, folderName, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(folder.FolderId)
            )
            ?.FolderId;
    }

    private async Task<string> GetAuthTokenAsync(
        AlfafileConfig config,
        CancellationToken cancellationToken
    )
    {
        return await authTokenCache.GetOrAuthenticateAsync(
            config.Username,
            async ct =>
            {
                logger.LogInformation(
                    "Authenticating to Alfafile for user {Username}",
                    config.Username
                );

                var response = await api.LoginAsync(
                    login: config.Username,
                    password: config.Password,
                    cancellationToken: ct
                );

                var content = response.Content;

                if (
                    !response.IsSuccessStatusCode
                    || content?.Status != (int)HttpStatusCode.OK
                    || string.IsNullOrWhiteSpace(content.Response?.Token)
                )
                {
                    throw new AuthenticationException(
                        content?.Details ?? $"Login failed with status code {response.StatusCode}"
                    );
                }

                return content.Response.Token;
            },
            cancellationToken
        );
    }

    private static string? GetFileId(string fileUrl)
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
}
