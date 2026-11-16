using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Hosters.FileFactory.Api.Requests;
using Bearcat.Hosters.FileFactory.Api.Responses;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Refit;

namespace Bearcat.Hosters.FileFactory.Api;

public class ApiClient(
    IFileFactoryWebApi webApi,
    IFileFactoryConvexApi convexApi,
    HttpClientProvider httpClientProvider,
    ILogger<ApiClient> logger
) : IFileFactoryApiClient
{
    public const string WebApiBaseUrl = "https://www.filefactory.com";

    public const string ConvexApiBaseUrl = "https://convex-ulta.filefactory.com";

    public const string FileLinkBaseUrl = "https://www.filefactory.com/file/";

    private const string SessionCookieName = "__Host-__convexAuthJWT";

    private const string MimeType = "application/octet-stream";

    private const string ValidLinkStatus = "valid";

    private const string InvalidLinkStatus = "invalid";

    private const string FileNotFoundErrorMessage = "File not found or has been deleted.";

    private const string ConvexErrorStatus = "error";

    private const int MaxLinksPerLinkCheckRequest = 100;

    private const int CompleteUploadAttempts = 7;

    private readonly KeyedAuthTokenCache authTokenCache = new(TimeSpan.FromMinutes(50));

    public TimeSpan MaximumCompleteUploadRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    public async Task<FileFactoryUploadResult> UploadFileAsync(
        FileFactoryConfig config,
        Stream stream,
        string fileName,
        long fileSize,
        string? folderId,
        CancellationToken cancellationToken
    )
    {
        var signedUpload = await SignUploadAsync(
            config: config,
            fileName: fileName,
            fileSize: fileSize,
            folderId: folderId,
            cancellationToken: cancellationToken
        );

        await PutFileAsync(signedUpload.Url!, stream, fileSize, cancellationToken);

        var completedUpload = await CompleteUploadAsync(
            config,
            signedUpload.SessionId!,
            cancellationToken
        );

        return new FileFactoryUploadResult(
            FileId: completedUpload.FileId,
            FileUrl: $"{FileLinkBaseUrl}{completedUpload.Viewhash}"
        );
    }

    public async Task<IReadOnlyDictionary<string, HostedFileInfo>> GetFileInfoPerFileIdAsync(
        FileFactoryConfig config,
        IReadOnlyList<string> fileIds,
        CancellationToken cancellationToken
    )
    {
        var fileInfoPerFileId = new Dictionary<string, HostedFileInfo>();

        foreach (var chunk in fileIds.Distinct().Chunk(MaxLinksPerLinkCheckRequest))
        {
            var fileIdPerFileLink = chunk.ToDictionary(fileId => $"{FileLinkBaseUrl}{fileId}");

            var results = await CheckLinksAsync(
                config: config,
                fileLinks: fileIdPerFileLink.Keys.ToList(),
                cancellationToken: cancellationToken
            );

            var resultPerFileLink = results
                .Where(result => result.OriginalUrl is not null)
                .ToDictionary(result => result.OriginalUrl!);

            foreach (var (fileLink, fileId) in fileIdPerFileLink)
            {
                if (!resultPerFileLink.TryGetValue(fileLink, out var result))
                {
                    throw new HttpRequestException(
                        $"FileFactory link check returned no result for {fileLink}"
                    );
                }

                fileInfoPerFileId[fileId] = ReadFileInfo(fileLink, result);
            }
        }

        return fileInfoPerFileId;
    }

    public async Task EnsureCredentialsAcceptedAsync(
        FileFactoryConfig config,
        CancellationToken cancellationToken
    )
    {
        var response = await authTokenCache.SendWithAuthTokenAsync(
            accountKey: config.Email,
            authenticateAsync: ct => SignInAsync(config, ct),
            sendRequestAsync: token =>
                convexApi.QueryCurrentUserAsync(
                    token: token,
                    request: new ConvexQueryRequest(
                        Path: "users:me",
                        Arguments: new Dictionary<string, string>(),
                        Format: "json"
                    ),
                    cancellationToken: cancellationToken
                ),
            isAuthTokenRejected: IsCurrentUserQueryUnauthenticated,
            cancellationToken: cancellationToken
        );

        if (!response.IsSuccessStatusCode || response.Content is null)
        {
            throw new HttpRequestException(
                $"FileFactory current user query failed with status code {response.StatusCode}: {GetErrorContent(response)}"
            );
        }

        if (response.Content.Status == ConvexErrorStatus)
        {
            throw new HttpRequestException(
                $"FileFactory current user query failed: {response.Content.ErrorMessage}"
            );
        }

        if (response.Content.Value is null)
        {
            throw new HttpRequestException(
                "FileFactory did not recognize the session of the successful login"
            );
        }
    }

    private async Task<SignUploadResponse> SignUploadAsync(
        FileFactoryConfig config,
        string fileName,
        long fileSize,
        string? folderId,
        CancellationToken cancellationToken
    )
    {
        var response = await SendWithSessionCookieAsync(
            config: config,
            sendRequestAsync: cookie =>
                webApi.SignUploadAsync(
                    cookie: cookie,
                    request: new SignUploadRequest(
                        UseSession: true,
                        FileName: fileName,
                        ContentType: MimeType,
                        Size: fileSize,
                        FolderId: folderId
                    ),
                    cancellationToken: cancellationToken
                ),
            cancellationToken: cancellationToken
        );

        if (
            !response.IsSuccessStatusCode
            || string.IsNullOrWhiteSpace(response.Content?.Url)
            || string.IsNullOrWhiteSpace(response.Content.SessionId)
        )
        {
            throw new HttpRequestException(
                $"FileFactory upload signing failed with status code {response.StatusCode}: {GetErrorContent(response)}"
            );
        }

        return response.Content;
    }

    private async Task PutFileAsync(
        string uploadUrl,
        Stream stream,
        long fileSize,
        CancellationToken cancellationToken
    )
    {
        using var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue(MimeType);
        content.Headers.ContentLength = fileSize;

        using var httpClient = httpClientProvider.GetUploadClient();
        using var response = await httpClient.PutAsync(uploadUrl, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new HttpRequestException(
                $"FileFactory file upload failed with status code {response.StatusCode}: {responseContent}"
            );
        }
    }

    private async Task<CompleteUploadResponse> CompleteUploadAsync(
        FileFactoryConfig config,
        string sessionId,
        CancellationToken cancellationToken
    )
    {
        for (var attemptIndex = 0; ; attemptIndex++)
        {
            var isLastAttempt = attemptIndex == CompleteUploadAttempts - 1;
            ApiResponse<CompleteUploadResponse> response;

            try
            {
                response = await SendWithSessionCookieAsync(
                    config: config,
                    sendRequestAsync: cookie =>
                        webApi.CompleteUploadAsync(
                            cookie: cookie,
                            request: new CompleteUploadRequest(sessionId),
                            cancellationToken: cancellationToken
                        ),
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception ex)
                when (ex is not HosterCredentialsRejectedException
                    && !cancellationToken.IsCancellationRequested
                    && !isLastAttempt
                )
            {
                logger.LogWarning(
                    ex,
                    "FileFactory upload completion request failed on attempt {Attempt}",
                    attemptIndex + 1
                );

                await Task.Delay(GetCompleteUploadRetryDelay(attemptIndex), cancellationToken);

                continue;
            }

            if (
                response is
                {
                    IsSuccessStatusCode: true,
                    Content: { Viewhash.Length: > 0, DownloadUrl.Length: > 0 }
                }
            )
            {
                return response.Content;
            }

            if (isLastAttempt || !IsRetryableStatusCode(response.StatusCode))
            {
                throw new HttpRequestException(
                    $"FileFactory upload completion failed with status code {response.StatusCode}: {GetErrorContent(response)}"
                );
            }

            logger.LogWarning(
                "FileFactory upload completion returned status code {StatusCode} on attempt {Attempt}",
                response.StatusCode,
                attemptIndex + 1
            );

            await Task.Delay(GetCompleteUploadRetryDelay(attemptIndex), cancellationToken);
        }
    }

    private TimeSpan GetCompleteUploadRetryDelay(int attemptIndex)
    {
        var delay = TimeSpan.FromSeconds(Math.Pow(2, attemptIndex));

        return delay < MaximumCompleteUploadRetryDelay ? delay : MaximumCompleteUploadRetryDelay;
    }

    private static bool IsRetryableStatusCode(HttpStatusCode? statusCode)
    {
        return statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            || (int?)statusCode >= 500;
    }

    private async Task<List<LinkCheckResultResponse>> CheckLinksAsync(
        FileFactoryConfig config,
        IReadOnlyList<string> fileLinks,
        CancellationToken cancellationToken
    )
    {
        var response = await SendWithSessionCookieAsync(
            config: config,
            sendRequestAsync: cookie =>
                webApi.CheckLinksAsync(
                    cookie: cookie,
                    request: new LinkCheckRequest(string.Join('\n', fileLinks)),
                    cancellationToken: cancellationToken
                ),
            cancellationToken: cancellationToken
        );

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var rateLimit = JsonSerializer.Deserialize<RateLimitResponse>(
                GetErrorContent(response) ?? string.Empty
            );

            throw new HttpRequestException(
                $"FileFactory link check is rate limited, retry after {rateLimit?.RetryAfter} seconds",
                inner: null,
                statusCode: response.StatusCode
            );
        }

        if (!response.IsSuccessStatusCode || response.Content?.Results is null)
        {
            throw new HttpRequestException(
                $"FileFactory link check failed with status code {response.StatusCode}: {GetErrorContent(response)}"
            );
        }

        return response.Content.Results;
    }

    private static HostedFileInfo ReadFileInfo(string fileLink, LinkCheckResultResponse result)
    {
        if (result.Status == ValidLinkStatus)
        {
            return new HostedFileInfo(
                IsOnline: true,
                FileName: result.FileDetails?.Name,
                SizeBytes: null
            );
        }

        if (result is { Status: InvalidLinkStatus, ErrorMessage: FileNotFoundErrorMessage })
        {
            return new HostedFileInfo(IsOnline: false, FileName: null, SizeBytes: null);
        }

        throw new HttpRequestException(
            $"FileFactory link check returned unknown status {result.Status} for {fileLink}: {result.ErrorMessage}"
        );
    }

    private Task<ApiResponse<TContent>> SendWithSessionCookieAsync<TContent>(
        FileFactoryConfig config,
        Func<string, Task<ApiResponse<TContent>>> sendRequestAsync,
        CancellationToken cancellationToken
    )
    {
        return authTokenCache.SendWithAuthTokenAsync(
            accountKey: config.Email,
            authenticateAsync: ct => SignInAsync(config, ct),
            sendRequestAsync: token => sendRequestAsync($"{SessionCookieName}={token}"),
            isAuthTokenRejected: response => response.StatusCode == HttpStatusCode.Unauthorized,
            cancellationToken: cancellationToken
        );
    }

    private static bool IsCurrentUserQueryUnauthenticated(
        ApiResponse<ConvexResponse<CurrentUserResponse>> response
    )
    {
        return response.StatusCode == HttpStatusCode.Unauthorized
            || response
                is {
                    IsSuccessStatusCode: true,
                    Content: { Status: not ConvexErrorStatus, Value: null }
                };
    }

    private async Task<string> SignInAsync(
        FileFactoryConfig config,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("Logging in to FileFactory for account {Email}", config.Email);

        var response = await webApi.SignInAsync(
            new SignInRequest(
                Action: "auth:signIn",
                Arguments: new SignInArguments(
                    Provider: "password",
                    Parameters: new SignInParameters(
                        Email: config.Email,
                        Password: config.Password,
                        Flow: "signIn"
                    )
                )
            ),
            cancellationToken
        );

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new HosterCredentialsRejectedException(
                $"FileFactory rejected the email or password: {GetErrorContent(response)}"
            );
        }

        if (!response.IsSuccessStatusCode || response.Content?.Tokens?.Token is not { Length: > 0 })
        {
            throw new HttpRequestException(
                $"FileFactory login failed with status code {response.StatusCode}: {GetErrorContent(response)}",
                inner: null,
                statusCode: response.StatusCode
            );
        }

        return response.Content.Tokens.Token;
    }

    private static string? GetErrorContent(IApiResponse response)
    {
        return (response.Error as ApiException)?.Content;
    }
}
