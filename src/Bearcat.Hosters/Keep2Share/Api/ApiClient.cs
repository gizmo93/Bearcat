using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Keep2Share.Api;

public class ApiClient(
    IKeep2ShareApi api,
    HttpClientProvider httpClientProvider,
    ILogger<ApiClient> logger
) : IKeep2ShareApiClient
{
    public const string UploadHttpClientName = "Keep2ShareUploadHttpClient";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
    };

    private const int AuthTimeout = 1500;

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
        IReadOnlyList<string> fileUrls,
        CancellationToken cancellationToken
    )
    {
        var result = new Dictionary<string, bool>();

        foreach (var fileUrl in fileUrls.Distinct())
        {
            var fileId = TryExtractFileId(fileUrl);

            if (fileId is null)
            {
                result[fileUrl] = false;
                continue;
            }

            try
            {
                var response = await api.GetFileStatusAsync(
                    new FileStatusRequest(fileId),
                    cancellationToken
                );

                result[fileUrl] = response.Status == "success" && response.IsAvailable == true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "Failed to check Keep2Share link {FileUrl}: {Message}",
                    fileUrl,
                    ex.InnerException?.Message ?? ex.Message
                );
                result[fileUrl] = false;
            }
        }

        return result;
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
