using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Hosters.Extensions;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Refit;

namespace Bearcat.Hosters.Keep2Share.Api;

public class ApiClient(
    IKeep2ShareApi api,
    HttpClientProvider httpClientProvider,
    ILogger<ApiClient> logger
) : IKeep2ShareApiClient
{
    public const string UploadHttpClientName = "Keep2ShareUploadHttpClient";

    public TimeSpan RateLimitRetryDelay { get; init; } = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
    };

    private const int AuthTimeout = 1500;

    private const int MaxFilesInfoBatchSize = 100;

    private const int MaxFilesInfoAttempts = 3;

    private bool NeedsReauthentication =>
        string.IsNullOrWhiteSpace(authToken)
        || (DateTime.UtcNow - lastAuthTime).TotalSeconds > AuthTimeout;

    private string? authToken;

    private DateTime lastAuthTime = DateTime.MinValue;

    private readonly SemaphoreSlim authSemaphore = new(initialCount: 1, maxCount: 1);

    public async Task<LoginResponse> LoginAsync(
        Keep2ShareConfig config,
        CancellationToken cancellationToken
    )
    {
        return await LoginAsync(
            config,
            null,
            null,
            throwOnCaptchaRequired: true,
            cancellationToken
        );
    }

    public async Task<CaptchaChallengeResult> RequestCaptchaChallengeAsync(
        CancellationToken cancellationToken
    )
    {
        var response = await api.RequestReCaptchaAsync(cancellationToken);

        if (
            response.Status == "success"
            && !string.IsNullOrWhiteSpace(response.Challenge)
            && !string.IsNullOrWhiteSpace(response.CaptchaUrl)
        )
        {
            return new CaptchaChallengeResult(
                IsSuccess: true,
                Challenge: response.Challenge,
                CaptchaUrl: response.CaptchaUrl
            );
        }

        return new CaptchaChallengeResult(
            IsSuccess: false,
            ErrorMessage: response.Message
                ?? $"Keep2Share captcha challenge request failed with status={response.Status}, code={response.Code}, errorCode={response.ErrorCode?.ToString() ?? "null"}"
        );
    }

    public async Task<TryLoginResult> VerifyCaptchaAsync(
        Keep2ShareConfig config,
        string challenge,
        string response,
        CancellationToken cancellationToken
    )
    {
        var loginResponse = await LoginAsync(
            config,
            challenge,
            response,
            throwOnCaptchaRequired: false,
            cancellationToken
        );

        return new TryLoginResult(
            IsSuccess: loginResponse.Status == "success"
                && loginResponse.Code == (int)HttpStatusCode.OK,
            ErrorMessage: loginResponse.Status == "success"
                ? null
                : loginResponse.Message
                    ?? $"Keep2Share login failed with status={loginResponse.Status}, code={loginResponse.Code}, errorCode={loginResponse.ErrorCode?.ToString() ?? "null"}"
        );
    }

    public async Task<AccountInfoResponse> GetAccountInfoAsync(
        Keep2ShareConfig config,
        CancellationToken cancellationToken
    )
    {
        var token = await GetAuthTokenAsync(config, cancellationToken);
        return await api.GetAccountInfoAsync(new AuthenticatedRequest(token), cancellationToken);
    }

    public async Task<UploadFormDataResponse> RequestUploadAsync(
        Keep2ShareConfig config,
        string? parentId,
        CancellationToken cancellationToken
    )
    {
        var token = await GetAuthTokenAsync(config, cancellationToken);
        UploadFormDataResponse response;

        try
        {
            response = await api.GetUploadFormDataAsync(
                new UploadFormDataRequest(token, parentId),
                cancellationToken
            );
        }
        catch (ApiException ex)
        {
            ThrowIfCaptchaVerificationRequired(ex);
            throw;
        }

        if (
            response.Status != "success"
            || string.IsNullOrWhiteSpace(response.FormAction)
            || string.IsNullOrWhiteSpace(response.FileField)
        )
        {
            ThrowIfCaptchaVerificationRequired(response.Code, response.ErrorCode, response.Message);

            throw new HttpRequestException(
                response.Message
                    ?? $"Keep2Share upload form request failed with status {response.Status}"
            );
        }

        return response;
    }

    public async Task<string> CreateFolderAsync(
        Keep2ShareConfig config,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        var token = await GetAuthTokenAsync(config, cancellationToken);
        var existingFolderId = await GetFolderIdAsync(token, folderName, cancellationToken);

        if (existingFolderId is not null)
        {
            return existingFolderId;
        }

        CreateFolderResponse response;

        try
        {
            response = await api.CreateFolderAsync(
                new CreateFolderRequest(
                    AuthToken: token,
                    Name: folderName,
                    Parent: "/",
                    Access: "public"
                ),
                cancellationToken
            );
        }
        catch (ApiException ex)
        {
            ThrowIfCaptchaVerificationRequired(ex);
            throw;
        }

        if (response.Status != "success" || !((HttpStatusCode)response.Code).IsSuccessStatusCode)
        {
            ThrowIfCaptchaVerificationRequired(response.Code, null, response.Message);

            throw new HttpRequestException(
                response.Message
                    ?? $"Keep2Share folder creation failed with status={response.Status}, code={response.Code}"
            );
        }

        return response.Id
            ?? throw new HttpRequestException("Keep2Share folder creation returned no folder id");
    }

    public async Task<UploadFileResponse> UploadFileAsync(
        UploadFormDataResponse uploadFormData,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken
    )
    {
        using var httpClient = httpClientProvider.GetClient(UploadHttpClientName);
        using var multipartForm = new MultipartFormDataContent();

        foreach (var (key, value) in uploadFormData.FormData)
        {
            multipartForm.Add(new StringContent(ConvertFormDataValue(value)), key);
        }

        multipartForm.Add(
            new StreamContent(stream),
            uploadFormData.FileField!,
            Path.GetFileName(fileName)
        );

        var httpResponse = await httpClient.PostAsync(
            uploadFormData.FormAction!,
            multipartForm,
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
        );

        return response
            ?? throw new HttpRequestException($"Upload response for file {fileName} was empty");
    }

    public async Task<IReadOnlyDictionary<string, bool>> CheckLinksAsync(
        Keep2ShareConfig config,
        IReadOnlyList<string> fileUrls,
        CancellationToken cancellationToken
    )
    {
        var token = await GetAuthTokenAsync(config, cancellationToken);
        var statusPerFileUrl = fileUrls.Distinct().ToDictionary(fileUrl => fileUrl, _ => false);

        var fileUrlsByFileId = statusPerFileUrl
            .Keys.Select(fileUrl => new { FileUrl = fileUrl, FileId = TryExtractFileId(fileUrl) })
            .Where(file => file.FileId is not null)
            .GroupBy(file => file.FileId!)
            .ToDictionary(group => group.Key, group => group.Select(file => file.FileUrl).ToList());

        foreach (var fileIdBatch in fileUrlsByFileId.Keys.Chunk(MaxFilesInfoBatchSize))
        {
            var response = await GetFilesInfoAsync(token, fileIdBatch, cancellationToken);

            if (response.Status != "success")
            {
                ThrowIfCaptchaVerificationRequired(
                    response.Code,
                    response.ErrorCode,
                    response.Message
                );

                throw new HttpRequestException(
                    $"Keep2Share files info request failed with status={response.Status}, code={response.Code}, errorCode={response.ErrorCode?.ToString() ?? "null"}, message={response.Message ?? "null"}"
                );
            }

            foreach (var file in response.Files.Where(file => file.Id is not null))
            {
                if (!fileUrlsByFileId.TryGetValue(file.Id, out var urls))
                {
                    continue;
                }

                foreach (var url in urls)
                {
                    statusPerFileUrl[url] = file.IsAvailable == true;
                }
            }
        }

        return statusPerFileUrl;
    }

    private async Task<GetFilesInfoResponse> GetFilesInfoAsync(
        string token,
        IReadOnlyList<string> fileIds,
        CancellationToken cancellationToken
    )
    {
        foreach (var attempt in Enumerable.Range(1, MaxFilesInfoAttempts))
        {
            try
            {
                return await api.GetFilesInfoAsync(
                    new GetFilesInfoRequest(token, fileIds),
                    cancellationToken
                );
            }
            catch (ApiException exception)
                when (exception.StatusCode == HttpStatusCode.TooManyRequests)
            {
                logger.LogInformation(
                    "Rate limited by Keep2Share API while checking file batch, waiting before retrying (Attempt {Attempt})",
                    attempt
                );
            }
            catch (ApiException exception)
            {
                ThrowIfCaptchaVerificationRequired(exception);
                throw;
            }
            catch (HttpRequestException exception)
                when (exception.StatusCode == HttpStatusCode.TooManyRequests)
            {
                logger.LogInformation(
                    "Rate limited by Keep2Share API while checking file batch, waiting before retrying (Attempt {Attempt})",
                    attempt
                );
            }

            if (attempt < MaxFilesInfoAttempts)
            {
                await Task.Delay(RateLimitRetryDelay, cancellationToken);
            }
        }

        throw new HttpRequestException("Rate limited by Keep2Share API while checking file batch");
    }

    private async Task<string?> GetFolderIdAsync(
        string token,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        FolderListResponse response;

        try
        {
            response = await api.GetFoldersListAsync(
                new FolderListRequest(token),
                cancellationToken
            );
        }
        catch (ApiException ex)
        {
            ThrowIfCaptchaVerificationRequired(ex);
            throw;
        }

        if (response.Status != "success" || !((HttpStatusCode)response.Code).IsSuccessStatusCode)
        {
            ThrowIfCaptchaVerificationRequired(response.Code, null, response.Message);

            throw new HttpRequestException(
                response.Message
                    ?? $"Keep2Share folder list failed with status={response.Status}, code={response.Code}"
            );
        }

        foreach (
            var (folder, index) in response.FoldersList.Select((folder, index) => (folder, index))
        )
        {
            if (
                FolderNameMatches(folder, folderName)
                && response.FoldersIds.Count > index
                && !string.IsNullOrWhiteSpace(response.FoldersIds[index])
            )
            {
                return response.FoldersIds[index];
            }
        }

        return null;
    }

    private static bool FolderNameMatches(string actualName, string expectedName)
    {
        return string.Equals(actualName, expectedName, StringComparison.Ordinal)
            || string.Equals(actualName, $"/{expectedName}", StringComparison.Ordinal);
    }

    private async Task<string> GetAuthTokenAsync(
        Keep2ShareConfig config,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await authSemaphore.WaitAsync(cancellationToken);

            if (NeedsReauthentication)
            {
                logger.LogInformation(
                    "Authenticating to Keep2Share for user {Username}",
                    config.EmailAddress
                );

                var loginResponse = await LoginAsync(config, cancellationToken);

                if (loginResponse.Status != "success" || loginResponse.AuthToken is null)
                {
                    throw new HttpRequestException(
                        loginResponse.Message
                            ?? $"Keep2Share login failed with status code {loginResponse.Code}"
                    );
                }
            }

            return authToken!;
        }
        finally
        {
            authSemaphore.Release();
        }
    }

    private async Task<LoginResponse> LoginAsync(
        Keep2ShareConfig config,
        string? reCaptchaChallenge,
        string? reCaptchaResponse,
        bool throwOnCaptchaRequired,
        CancellationToken cancellationToken
    )
    {
        LoginResponse? response = null;

        try
        {
            response = await api.LoginAsync(
                new LoginRequest(
                    config.EmailAddress,
                    config.Password,
                    reCaptchaChallenge,
                    reCaptchaResponse
                ),
                cancellationToken
            );
        }
        catch (ApiException ex)
            when (TryDeserializeLoginResponse(ex, out response)
                || ex.StatusCode == HttpStatusCode.NotAcceptable
            )
        {
            response ??= new LoginResponse
            {
                Status = "error",
                Code = (int)ex.StatusCode,
                Message = ex.Message,
            };
        }

        if (response.Status == "success" && !string.IsNullOrWhiteSpace(response.AuthToken))
        {
            authToken = response.AuthToken;
            lastAuthTime = DateTime.UtcNow;
            return response;
        }

        if (
            throwOnCaptchaRequired
            && IsCaptchaVerificationRequired(response.Code, response.ErrorCode)
        )
        {
            throw new CaptchaVerificationRequiredException(
                response.Message ?? "Keep2Share requires captcha verification.",
                response.Code,
                response.ErrorCode
            );
        }

        return response;
    }

    private static void ThrowIfCaptchaVerificationRequired(ApiException exception)
    {
        if (TryDeserializeLoginResponse(exception, out var response))
        {
            var code = response!.Code != 0 ? response.Code : (int)exception.StatusCode;

            ThrowIfCaptchaVerificationRequired(code, response.ErrorCode, response.Message);
            return;
        }

        if (exception.StatusCode == HttpStatusCode.NotAcceptable)
        {
            throw new CaptchaVerificationRequiredException(
                "Keep2Share requires captcha verification.",
                (int)exception.StatusCode
            );
        }
    }

    private static void ThrowIfCaptchaVerificationRequired(
        int code,
        int? errorCode,
        string? message
    )
    {
        if (!IsCaptchaVerificationRequired(code, errorCode))
        {
            return;
        }

        throw new CaptchaVerificationRequiredException(
            message ?? "Keep2Share requires captcha verification.",
            code,
            errorCode
        );
    }

    private static bool IsCaptchaVerificationRequired(int code, int? errorCode)
    {
        return code == (int)HttpStatusCode.NotAcceptable && errorCode is null or 33
            || code == (int)HttpStatusCode.BadRequest && errorCode == 2;
    }

    private static bool TryDeserializeLoginResponse(
        ApiException exception,
        out LoginResponse? response
    )
    {
        response = null;

        if (string.IsNullOrWhiteSpace(exception.Content))
        {
            return false;
        }

        try
        {
            response = JsonSerializer.Deserialize<LoginResponse>(
                exception.Content,
                JsonSerializerOptions
            );
            return response is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? TryExtractFileId(string fileUrl)
    {
        if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var segments = uri
            .AbsolutePath.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
            .ToList();

        var fileSegmentIndex = segments.FindIndex(segment =>
            string.Equals(segment, "file", StringComparison.OrdinalIgnoreCase)
        );

        if (fileSegmentIndex >= 0 && segments.Count > fileSegmentIndex + 1)
        {
            return segments[fileSegmentIndex + 1];
        }

        return segments.LastOrDefault();
    }

    private static string ConvertFormDataValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            JsonValueKind.Undefined => string.Empty,
            _ => value.GetRawText(),
        };
    }
}
