using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Hosters.GoFile.Api.CreateFolder;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Refit;
using Response = Bearcat.Hosters.GoFile.Api.GetAccountId.Response;

namespace Bearcat.Hosters.GoFile.Api;

public class ApiClient(
    IGoFileApi api,
    HttpClientProvider httpClientProvider,
    ILogger<ApiClient> logger
) : IGoFileApiClient
{
    public TimeSpan RateLimitRetryDelay { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan FileCheckTimeout { get; init; } = TimeSpan.FromSeconds(30);

    private const string UploadUrl = "https://upload.gofile.io/uploadfile";

    private const int MaxParallelLinkChecks = 5;

    private const int MaxLinkCheckAttempts = 3;

    private const string UserAgent = "Bearcat";

    private const string WrongTokenStatus = "error-wrongToken";

    public async Task<Response> GetAccountAsync(
        string apiKey,
        CancellationToken cancellationToken = default
    )
    {
        return await SendApiRequestAsync(() =>
            api.GetAccountAsync(GetAuthorizationHeader(apiKey), cancellationToken)
        );
    }

    public async Task<UploadFile.Response> UploadFileAsync(
        string apiKey,
        Stream fileStream,
        string fileName,
        string folderId,
        CancellationToken cancellationToken
    )
    {
        using var httpClient = httpClientProvider.GetUploadClient();

        var request = new HttpRequestMessage(HttpMethod.Post, requestUri: UploadUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var fileContent = new StreamContent(fileStream);

        var multipartContent = new MultipartFormDataContent
        {
            { fileContent, "file", fileName },
            { new StringContent(folderId), "folderId" },
        };

        request.Content = multipartContent;

        var response = await httpClient.SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        ThrowIfApiTokenRejected(response.StatusCode, responseContent);

        response.EnsureSuccessStatusCode();

        return JsonSerializer.Deserialize<UploadFile.Response>(responseContent)!;
    }

    public async Task<string> CreateUploadFolderIdAsync(
        string apiKey,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        var apiToken = GetAuthorizationHeader(apiKey);
        var account = await SendApiRequestAsync(() =>
            api.GetAccountAsync(apiToken, cancellationToken)
        );

        if (
            !string.Equals(account.Status, "ok", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(account.Data?.Id)
        )
        {
            throw new HttpRequestException(
                $"GoFile account lookup failed with status {account.Status}"
            );
        }

        var accountInfos = await SendApiRequestAsync(() =>
            api.GetAccountInfosAsync(account.Data.Id, apiToken, cancellationToken)
        );

        if (
            !string.Equals(accountInfos.Status, "ok", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(accountInfos.Data?.RootFolder)
        )
        {
            throw new HttpRequestException(
                $"GoFile account info lookup failed with status {accountInfos.Status}"
            );
        }

        var existingFolderId = await GetExistingRootFolderIdAsync(
            rootFolderId: accountInfos.Data.RootFolder,
            folderName: folderName,
            apiKey: apiKey,
            cancellationToken: cancellationToken
        );

        if (existingFolderId is not null)
        {
            return existingFolderId;
        }

        var folder = await SendApiRequestAsync(() =>
            api.CreateFolderAsync(
                apiToken,
                new Request(accountInfos.Data.RootFolder, folderName),
                cancellationToken
            )
        );

        if (
            !string.Equals(folder.Status, "ok", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(folder.Data?.Id)
        )
        {
            throw new HttpRequestException(
                $"GoFile folder creation failed with status {folder.Status}"
            );
        }

        return folder.Data.Id;
    }

    public async Task MoveFileToFolderAsync(
        string apiKey,
        string fileUrl,
        string folderId,
        CancellationToken cancellationToken
    )
    {
        var fileId = TryExtractFileId(fileUrl);

        if (string.IsNullOrWhiteSpace(fileId))
        {
            throw new HttpRequestException($"Could not extract GoFile file id from URL {fileUrl}");
        }

        var response = await SendApiRequestAsync(() =>
            api.MoveContentAsync(
                GetAuthorizationHeader(apiKey),
                new MoveContent.Request(ContentsId: fileId, FolderId: folderId),
                cancellationToken
            )
        );

        if (!string.Equals(response.Status, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new HttpRequestException(
                $"GoFile file move failed with status {response.Status}"
            );
        }
    }

    private async Task<string?> GetExistingRootFolderIdAsync(
        string rootFolderId,
        string folderName,
        string apiKey,
        CancellationToken cancellationToken
    )
    {
        GetContent.Response rootFolder;

        try
        {
            rootFolder = await SendApiRequestAsync(() =>
                api.GetContentAsync(
                    folderId: rootFolderId,
                    apiToken: GetAuthorizationHeader(apiKey),
                    userAgent: UserAgent,
                    contentFilter: folderName,
                    sortField: "createTime",
                    sortDirection: 1,
                    cancellationToken: cancellationToken
                )
            );
        }
        catch (ApiException exception) when (IsNotPremiumError(exception))
        {
            logger.LogInformation(
                exception,
                "GoFile content listing requires premium, skipping duplicate folder detection"
            );

            return null;
        }

        if (!string.Equals(rootFolder.Status, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new HttpRequestException(
                $"GoFile root folder lookup failed with status {rootFolder.Status}"
            );
        }

        return rootFolder
            .Data?.Children.Values.FirstOrDefault(content =>
                string.Equals(content.Type, "folder", StringComparison.OrdinalIgnoreCase)
                && string.Equals(content.Name, folderName, StringComparison.Ordinal)
            )
            ?.Id;
    }

    private static bool IsNotPremiumError(ApiException exception)
    {
        return exception.StatusCode == HttpStatusCode.Unauthorized
            && (exception.Content?.Contains("error-notPremium") ?? false);
    }

    public async Task<
        IReadOnlyDictionary<string, (bool IsOnline, string? ErrorMessage)>
    > CheckOnlineStatusAsync(
        IReadOnlyList<string> fileUrls,
        string apiKey,
        CancellationToken cancellationToken
    )
    {
        using var semaphore = new SemaphoreSlim(MaxParallelLinkChecks);

        var checkOnlineStatusTasks = fileUrls
            .Distinct()
            .Select(fileUrl =>
                CheckFileOnlineStatusAsync(
                    fileUrl: fileUrl,
                    apiKey: apiKey,
                    semaphore: semaphore,
                    cancellationToken: cancellationToken
                )
            )
            .ToList();

        await Task.WhenAll(checkOnlineStatusTasks);

        return checkOnlineStatusTasks
            .Select(task => task.Result)
            .ToDictionary(
                result => result.FileUrl,
                result => (result.IsOnline, result.ErrorMessage)
            );
    }

    private async Task<(
        string FileUrl,
        bool IsOnline,
        string? ErrorMessage
    )> CheckFileOnlineStatusAsync(
        string fileUrl,
        string apiKey,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken
    )
    {
        var fileId = TryExtractFileId(fileUrl);

        if (fileId is null)
        {
            return (fileUrl, IsOnline: false, ErrorMessage: "Invalid GoFile URL");
        }

        foreach (var attempt in Enumerable.Range(1, MaxLinkCheckAttempts))
        {
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                using var timeoutCancellationTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCancellationTokenSource.CancelAfter(FileCheckTimeout);

                GetFileInfo.Response response;

                try
                {
                    response = await SendApiRequestAsync(() =>
                        api.GetFileInfoAsync(
                            fileId: fileId,
                            apiToken: GetAuthorizationHeader(apiKey),
                            userAgent: UserAgent,
                            cancellationToken: timeoutCancellationTokenSource.Token
                        )
                    );
                }
                catch (ApiException exception) when (IsNotPremiumError(exception))
                {
                    logger.LogInformation(
                        exception,
                        "GoFile file check requires premium for {FileUrl}, assuming the file is online",
                        fileUrl
                    );

                    return (fileUrl, IsOnline: true, ErrorMessage: null);
                }

                var isOnline =
                    string.Equals(response.Status, "ok", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(
                        response.Data?.Type,
                        "file",
                        StringComparison.OrdinalIgnoreCase
                    );

                return (fileUrl, isOnline, null);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return (
                    FileUrl: fileUrl,
                    IsOnline: false,
                    ErrorMessage: $"GoFile file check timed out after {FormatTimeout(FileCheckTimeout)}"
                );
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ApiException exception)
                when (exception.StatusCode == HttpStatusCode.TooManyRequests)
            {
                logger.LogInformation(
                    exception,
                    "Rate limited by GoFile API while checking {FileUrl}, waiting before retrying (Attempt {Attempt})",
                    fileUrl,
                    attempt
                );
            }
            catch (Exception exception) when (exception is not HosterCredentialsRejectedException)
            {
                return (
                    FileUrl: fileUrl,
                    IsOnline: false,
                    ErrorMessage: exception.InnerException?.Message ?? exception.Message
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

        return (fileUrl, IsOnline: false, ErrorMessage: "Max retry attempts reached");
    }

    private static async Task<TResponse> SendApiRequestAsync<TResponse>(
        Func<Task<TResponse>> sendRequest
    )
    {
        try
        {
            return await sendRequest();
        }
        catch (ApiException exception)
        {
            ThrowIfApiTokenRejected(exception.StatusCode, exception.Content);

            throw;
        }
    }

    private static void ThrowIfApiTokenRejected(HttpStatusCode statusCode, string? responseContent)
    {
        if (statusCode != HttpStatusCode.Unauthorized || string.IsNullOrWhiteSpace(responseContent))
        {
            return;
        }

        string? status;

        try
        {
            using var document = JsonDocument.Parse(responseContent);
            status = document.RootElement.TryGetProperty("status", out var statusProperty)
                ? statusProperty.GetString()
                : null;
        }
        catch (JsonException)
        {
            return;
        }

        if (string.Equals(status, WrongTokenStatus, StringComparison.Ordinal))
        {
            throw new HosterCredentialsRejectedException(
                $"GoFile rejected the API token ({WrongTokenStatus})"
            );
        }
    }

    private static string? TryExtractFileId(string fileUrl)
    {
        var fileId = fileUrl
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();

        return string.IsNullOrWhiteSpace(fileId) ? null : fileId;
    }

    private static string GetAuthorizationHeader(string apiToken)
    {
        return $"Bearer {apiToken}";
    }

    private static string FormatTimeout(TimeSpan timeout)
    {
        return timeout.TotalSeconds >= 1
            ? $"{timeout.TotalSeconds:0} seconds"
            : $"{timeout.TotalMilliseconds:0} milliseconds";
    }
}
