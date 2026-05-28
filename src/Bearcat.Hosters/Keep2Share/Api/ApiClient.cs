using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        var response = await api.LoginAsync(
            new LoginRequest(config.EmailAddress, config.Password),
            cancellationToken
        );

        if (response.Status == "success" && !string.IsNullOrWhiteSpace(response.AuthToken))
        {
            authToken = response.AuthToken;
            lastAuthTime = DateTime.UtcNow;
        }

        return response;
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
        CancellationToken cancellationToken
    )
    {
        var token = await GetAuthTokenAsync(config, cancellationToken);
        var response = await api.GetUploadFormDataAsync(
            new UploadFormDataRequest(token),
            cancellationToken
        );

        if (
            response.Status != "success"
            || string.IsNullOrWhiteSpace(response.FormAction)
            || string.IsNullOrWhiteSpace(response.FileField)
        )
        {
            throw new HttpRequestException(
                response.Message
                    ?? $"Keep2Share upload form request failed with status {response.Status}"
            );
        }

        return response;
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
