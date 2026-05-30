using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Bearcat.Hosters.GoFile.Api.GetAccountId;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Refit;

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

    private const string WebsiteToken = "4fd6sg89d7s6";

    public async Task<Response> GetAccountAsync(
        string apiKey,
        CancellationToken cancellationToken = default
    )
    {
        return await api.GetAccountAsync(GetAuthorizationHeader(apiKey), cancellationToken);
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

        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<UploadFile.Response>(responseContent)!;
    }

    public async Task<string> CreateUploadFolderIdAsync(
        string apiKey,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        var apiToken = GetAuthorizationHeader(apiKey);
        var account = await api.GetAccountAsync(apiToken, cancellationToken);

        if (
            !string.Equals(account.Status, "ok", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(account.Data?.Id)
        )
        {
            throw new HttpRequestException(
                $"GoFile account lookup failed with status {account.Status}"
            );
        }

        var accountInfos = await api.GetAccountInfosAsync(
            account.Data.Id,
            apiToken,
            cancellationToken
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
            apiToken: apiToken,
            cancellationToken: cancellationToken
        );

        if (existingFolderId is not null)
        {
            return existingFolderId;
        }

        var folder = await api.CreateFolderAsync(
            apiToken,
            new CreateFolder.Request(accountInfos.Data.RootFolder, folderName),
            cancellationToken
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

    private async Task<string?> GetExistingRootFolderIdAsync(
        string rootFolderId,
        string folderName,
        string apiToken,
        CancellationToken cancellationToken
    )
    {
        var rootFolder = await api.GetContentAsync(
            folderId: rootFolderId,
            apiToken: apiToken,
            contentFilter: folderName,
            sortField: "createTime",
            sortDirection: 1,
            cancellationToken: cancellationToken
        );

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

    public async Task<
        IReadOnlyDictionary<string, (bool IsOnline, string? ErrorMessage)>
    > CheckOnlineStatusAsync(
        IReadOnlyList<string> fileUrls,
        string apiKey,
        CancellationToken cancellationToken
    )
    {
        using var semaphore = new SemaphoreSlim(MaxParallelLinkChecks);

        var apiToken = GetAuthorizationHeader(apiKey);

        var checkOnlineStatusTasks = fileUrls
            .Distinct()
            .Select(fileUrl =>
                CheckFileOnlineStatusAsync(
                    fileUrl: fileUrl,
                    apiToken: apiToken,
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
        string apiToken,
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

                var response = await api.GetFileInfoAsync(
                    fileId: fileId,
                    apiToken: apiToken,
                    websiteToken: WebsiteToken,
                    cancellationToken: timeoutCancellationTokenSource.Token
                );

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
                when (exception.StatusCode == HttpStatusCode.Unauthorized)
            {
                var notPremiumError = exception.Content?.Contains("error-notPremium") ?? false;

                if (notPremiumError)
                {
                    logger.LogInformation(
                        "GoFile API returned notPremium error. Assuming all files are online"
                    );
                    return (fileUrl, IsOnline: true, ErrorMessage: null);
                }

                return (
                    FileUrl: fileUrl,
                    IsOnline: false,
                    ErrorMessage: exception.InnerException?.Message ?? exception.Message
                );
            }
            catch (ApiException exception)
                when (exception.StatusCode == HttpStatusCode.TooManyRequests)
            {
                logger.LogInformation(
                    "Rate limited by GoFile API while checking {FileUrl}, waiting before retrying (Attempt {Attempt})",
                    fileUrl,
                    attempt
                );
            }
            catch (Exception exception)
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
