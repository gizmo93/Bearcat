using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.KrakenFiles.Api;

public class ApiClient(
    IKrakenFilesApi api,
    HttpClientProvider httpClientProvider,
    ILogger<ApiClient> logger
) : IKrakenFilesApiClient
{
    private const int MaxParallelLinkChecks = 5;

    private const string LoginProbeHash = "bearcat-login-check";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<UploadFileResponse> UploadFileAsync(
        KrakenFilesConfig config,
        Stream stream,
        string fileName,
        string? folderId,
        CancellationToken cancellationToken
    )
    {
        var uploadServer = await api.GetAvailableServerAsync(cancellationToken);

        if (
            uploadServer.Status != (int)HttpStatusCode.OK
            || string.IsNullOrWhiteSpace(uploadServer.Data?.Url)
            || string.IsNullOrWhiteSpace(uploadServer.Data.ServerAccessToken)
        )
        {
            throw new HttpRequestException(
                uploadServer.Data?.Message
                    ?? $"KrakenFiles upload server request failed with status {uploadServer.Status}"
            );
        }

        using var httpClient = httpClientProvider.GetUploadClient();
        using var multipartContent = new MultipartFormDataContent();
        multipartContent.Add(
            new StringContent(uploadServer.Data.ServerAccessToken),
            "serverAccessToken"
        );

        if (!string.IsNullOrWhiteSpace(folderId))
        {
            multipartContent.Add(new StringContent(folderId), "folderId");
        }

        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        multipartContent.Add(fileContent, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, uploadServer.Data.Url);
        request.Content = multipartContent;
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-AUTH-TOKEN", config.ApiKey);

        var httpResponse = await httpClient.SendAsync(request, cancellationToken);
        var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"KrakenFiles upload request failed with status code {httpResponse.StatusCode}: {content}"
            );
        }

        var response = JsonSerializer.Deserialize<UploadFileResponse>(
            content,
            JsonSerializerOptions
        );

        if (response is null)
        {
            throw new HttpRequestException("KrakenFiles upload response was empty");
        }

        if (
            response.Status != (int)HttpStatusCode.OK
            || string.IsNullOrWhiteSpace(response.Data?.Url)
        )
        {
            throw new HttpRequestException(
                response.Data?.Message ?? $"KrakenFiles upload failed with status {response.Status}"
            );
        }

        return response;
    }

    public async Task<string> CreateFolderAsync(
        KrakenFilesConfig config,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        var existingFolderId = await GetFolderIdAsync(config.ApiKey, folderName, cancellationToken);

        if (existingFolderId is not null)
        {
            return existingFolderId;
        }

        var createResponse = await api.CreateFolderAsync(
            config.ApiKey,
            new CreateFolderRequest(folderName),
            cancellationToken
        );

        if (createResponse.Status != (int)HttpStatusCode.OK)
        {
            throw new HttpRequestException(
                createResponse.Data?.Message
                    ?? $"KrakenFiles folder creation failed with status {createResponse.Status}"
            );
        }

        var createdFolderId = await GetFolderIdAsync(config.ApiKey, folderName, cancellationToken);

        return createdFolderId
            ?? throw new HttpRequestException("KrakenFiles folder creation returned no folder id");
    }

    public async Task MoveFileToFolderAsync(
        KrakenFilesConfig config,
        string fileUrl,
        string folderId,
        CancellationToken cancellationToken
    )
    {
        var fileHash = TryExtractFileHash(fileUrl);

        if (string.IsNullOrWhiteSpace(fileHash))
        {
            throw new HttpRequestException(
                $"Could not extract KrakenFiles file hash from URL {fileUrl}"
            );
        }

        var response = await api.MoveFileAsync(
            fileHash,
            config.ApiKey,
            new MoveFileRequest(folderId),
            cancellationToken
        );

        if (response.Status != (int)HttpStatusCode.OK)
        {
            throw new HttpRequestException(
                response.Data?.Message
                    ?? $"KrakenFiles file move failed with status {response.Status}"
            );
        }
    }

    public async Task<IReadOnlyDictionary<string, bool>> CheckLinksAsync(
        KrakenFilesConfig config,
        IReadOnlyList<string> fileUrls,
        CancellationToken cancellationToken
    )
    {
        if (!await IsApiKeyValidAsync(config, cancellationToken))
        {
            throw new HttpRequestException("Invalid credentials");
        }

        using var semaphore = new SemaphoreSlim(MaxParallelLinkChecks);

        var checkTasks = fileUrls
            .Distinct()
            .Select(fileUrl => CheckLinkAsync(config, fileUrl, semaphore, cancellationToken))
            .ToList();

        var results = await Task.WhenAll(checkTasks);

        return results
            .Where(result => result.IsOnline.HasValue)
            .ToDictionary(result => result.FileUrl, result => result.IsOnline!.Value);
    }

    public async Task<bool> IsApiKeyValidAsync(
        KrakenFilesConfig config,
        CancellationToken cancellationToken
    )
    {
        var response = await api.GetFileAsync(LoginProbeHash, config.ApiKey, cancellationToken);

        return response.StatusCode == HttpStatusCode.NotFound
            || response.StatusCode == HttpStatusCode.OK;
    }

    private async Task<(string FileUrl, bool? IsOnline)> CheckLinkAsync(
        KrakenFilesConfig config,
        string fileUrl,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken
    )
    {
        var fileHash = TryExtractFileHash(fileUrl);

        if (fileHash is null)
        {
            return (fileUrl, null);
        }

        await semaphore.WaitAsync(cancellationToken);

        try
        {
            var response = await api.GetFileAsync(fileHash, config.ApiKey, cancellationToken);

            return (fileUrl, response.StatusCode == HttpStatusCode.OK);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to check KrakenFiles link {FileUrl}: {Message}",
                fileUrl,
                ex.InnerException?.Message ?? ex.Message
            );

            return (fileUrl, null);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<string?> GetFolderIdAsync(
        string apiKey,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        var folderList = await api.ListFoldersAsync(apiKey, cancellationToken);

        if (folderList.Status != (int)HttpStatusCode.OK)
        {
            throw new HttpRequestException(
                folderList.Message
                    ?? $"KrakenFiles folder list failed with status {folderList.Status}"
            );
        }

        return folderList
            .Data?.FirstOrDefault(folder =>
                string.Equals(folder.Name, folderName, StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(folder.ParentId)
                && !string.IsNullOrWhiteSpace(folder.Id)
            )
            ?.Id;
    }

    private static string? TryExtractFileHash(string fileUrl)
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

        var viewSegmentIndex = segments.FindIndex(segment =>
            string.Equals(segment, "view", StringComparison.OrdinalIgnoreCase)
        );

        if (viewSegmentIndex >= 0 && segments.Count > viewSegmentIndex + 1)
        {
            return segments[viewSegmentIndex + 1];
        }

        return segments.LastOrDefault()?.Replace(".html", string.Empty);
    }
}
