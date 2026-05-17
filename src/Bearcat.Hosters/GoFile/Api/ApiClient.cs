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
    private const string UploadUrl = "https://upload.gofile.io/uploadfile";

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
        CancellationToken cancellationToken
    )
    {
        using var httpClient = httpClientProvider.GetUploadClient();

        var request = new HttpRequestMessage(HttpMethod.Post, requestUri: UploadUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var fileContent = new StreamContent(fileStream);

        var multipartContent = new MultipartFormDataContent { { fileContent, "file", fileName } };

        request.Content = multipartContent;

        var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<UploadFile.Response>(responseContent)!;
    }

    public async Task<
        IReadOnlyDictionary<string, (bool IsOnline, string? ErrorMessage)>
    > CheckOnlineStatusAsync(
        IReadOnlyList<string> fileUrls,
        string apiKey,
        CancellationToken cancellationToken
    )
    {
        using var semaphore = new SemaphoreSlim(3);

        var apiToken = GetAuthorizationHeader(apiKey);

        var checkOnlineStatusTasks = fileUrls
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
        foreach (var attempt in Enumerable.Range(1, 3))
        {
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                var fileId = fileUrl.Split("/").Last();
                var response = await api.GetOnlineStatusAsync(
                    fileId: fileId,
                    apiToken: apiToken,
                    cancellationToken: cancellationToken
                );

                var isOnline = response.Status != "error-notFound";

                return (fileUrl, isOnline, null);
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
                    "Rate limited by GoFile API, waiting 5 seconds before retrying (Attempt {Attempt})",
                    attempt
                );
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
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
        }

        return (fileUrl, IsOnline: false, ErrorMessage: "Max retry attempts reached");
    }

    private static string GetAuthorizationHeader(string apiToken)
    {
        return $"Bearer {apiToken}";
    }
}
