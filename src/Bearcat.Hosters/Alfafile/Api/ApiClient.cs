using System.Net;
using System.Security.Authentication;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Hosters.Alfafile.Api.File;
using Bearcat.Hosters.Alfafile.Api.Folder;
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

    private const int AuthTimeout = 400;

    private const int MaxParallelLinkChecks = 10;

    private const int MaxLinkCheckAttempts = 3;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
    };

    private bool NeedsReauthentication =>
        string.IsNullOrWhiteSpace(authToken)
        || (DateTime.UtcNow - lastAuthTime).TotalSeconds > AuthTimeout;

    private string? authToken;

    private DateTime lastAuthTime = DateTime.MinValue;

    private readonly SemaphoreSlim authSemaphore = new(initialCount: 1, maxCount: 1);

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

    public async Task<IReadOnlyDictionary<string, bool>> CheckLinksAsync(
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

        return results.ToDictionary(result => result.FileUrl, result => result.IsOnline);
    }

    private async Task<(string FileUrl, bool IsOnline)> CheckLinkAsync(
        string token,
        string fileUrl,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken
    )
    {
        var fileId = GetFileId(fileUrl);

        if (string.IsNullOrWhiteSpace(fileId))
        {
            return (fileUrl, false);
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
                    var content = response.Content;

                    return (
                        fileUrl,
                        response.IsSuccessStatusCode
                            && content
                                is { Status: (int)HttpStatusCode.OK, Response.File: not null }
                    );
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

        return (fileUrl, false);
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
        try
        {
            await authSemaphore.WaitAsync(cancellationToken);

            if (NeedsReauthentication)
            {
                logger.LogInformation(
                    "Authenticating to Alfafile for user {Username}",
                    config.Username
                );

                var response = await api.LoginAsync(
                    login: config.Username,
                    password: config.Password,
                    cancellationToken: cancellationToken
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

                authToken = content.Response.Token;
                lastAuthTime = DateTime.UtcNow;
            }

            return authToken!;
        }
        finally
        {
            authSemaphore.Release();
        }
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
