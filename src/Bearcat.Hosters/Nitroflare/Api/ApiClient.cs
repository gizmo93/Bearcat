using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Hosters.Nitroflare.Api.File;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Nitroflare.Api;

public class ApiClient(
    INitroflareApi api,
    HttpClientProvider httpClientProvider,
    ILogger<ApiClient> logger
) : INitroflareApiClient
{
    private const int MaxFileIdsPerFileInfoRequest = 100;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<UploadFileResponse> UploadFileAsync(
        NitroflareConfig config,
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken
    )
    {
        var uploadUrl = await GetUploadUrlAsync(cancellationToken);

        using var httpClient = httpClientProvider.GetUploadClient();
        using var multipartContent = new MultipartFormDataContent();
        multipartContent.Add(new StringContent(config.UserHash), "user");
        multipartContent.Add(new StreamContent(fileStream), "files", fileName);

        var httpResponse = await httpClient.PostAsync(
            uploadUrl,
            multipartContent,
            cancellationToken
        );

        var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Upload request failed with status code {httpResponse.StatusCode} for file {fileName}: {content}"
            );
        }

        var uploadResponse = DeserializeUploadResponse(content);
        var uploadedFile = uploadResponse.Files?.FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(uploadedFile?.Error))
        {
            throw new HttpRequestException(
                $"Upload failed for file {fileName}: {uploadedFile.Error}"
            );
        }

        if (string.IsNullOrWhiteSpace(uploadedFile?.Url))
        {
            throw new HttpRequestException(
                $"Upload failed for file {fileName}: no download URL returned"
            );
        }

        return uploadResponse;
    }

    public async Task<UploadFileResponse> TestUserHashAsync(
        NitroflareConfig config,
        CancellationToken cancellationToken
    )
    {
        var testFileName = $"bearcat-login-test-{Guid.NewGuid():N}.txt";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("x"));

        return await UploadFileAsync(
            config: config,
            fileStream: stream,
            fileName: testFileName,
            cancellationToken: cancellationToken
        );
    }

    public async Task<IReadOnlyDictionary<string, bool>> CheckLinksAsync(
        IReadOnlyList<string> fileUrls,
        CancellationToken cancellationToken
    )
    {
        var fileIdsByUrl = fileUrls
            .Distinct()
            .ToDictionary(fileUrl => fileUrl, GetFileId);

        var result = fileIdsByUrl.ToDictionary(
            item => item.Key,
            _ => false
        );

        var validFileIds = fileIdsByUrl
            .Values.OfType<string>()
            .Where(fileId => !string.IsNullOrWhiteSpace(fileId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var fileIdsBatch in validFileIds.Chunk(MaxFileIdsPerFileInfoRequest))
        {
            var fileIdsInCurrentBatch = fileIdsBatch.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var response = await api.GetFileInfoAsync(
                files: string.Join(',', fileIdsBatch),
                cancellationToken: cancellationToken
            );

            if (!response.IsSuccessStatusCode || response.Content is null)
            {
                logger.LogInformation(
                    "Nitroflare file info request failed for {FileIds} with status code {StatusCode}",
                    string.Join(',', fileIdsBatch),
                    response.StatusCode
                );

                continue;
            }

            if (!string.Equals(response.Content.Type, "success", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation(
                    "Nitroflare file info request failed for {FileIds}: {Message}",
                    string.Join(',', fileIdsBatch),
                    response.Content.Message
                );

                continue;
            }

            var onlineFileIds = response
                .Content.Result?.Files?.Where(file =>
                    string.Equals(
                        file.Value.Status,
                        "online",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                .Select(file => file.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

            foreach (var (fileUrl, fileId) in fileIdsByUrl)
            {
                if (fileId is not null && fileIdsInCurrentBatch.Contains(fileId))
                {
                    result[fileUrl] = onlineFileIds.Contains(fileId);
                }
            }
        }

        return result;
    }

    private async Task<string> GetUploadUrlAsync(CancellationToken cancellationToken)
    {
        var uploadUrl = await api.GetUploadServerAsync(cancellationToken);

        if (
            string.IsNullOrWhiteSpace(uploadUrl)
            || !Uri.TryCreate(uploadUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
        )
        {
            throw new HttpRequestException($"Nitroflare returned an invalid upload URL: {uploadUrl}");
        }

        return uploadUrl;
    }

    private static UploadFileResponse DeserializeUploadResponse(string content)
    {
        try
        {
            return JsonSerializer.Deserialize<UploadFileResponse>(content, JsonSerializerOptions)
                ?? throw new JsonException("Empty upload response");
        }
        catch (JsonException ex)
        {
            if (!content.TrimStart().StartsWith('{'))
            {
                throw new HttpRequestException(
                    $"Nitroflare returned an unexpected response: {content}"
                );
            }

            throw new HttpRequestException($"Nitroflare returned an unexpected response: {content}", ex);
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

        var viewSegmentIndex = Array.FindIndex(
            segments,
            segment => string.Equals(segment, "view", StringComparison.OrdinalIgnoreCase)
        );

        return viewSegmentIndex >= 0 && viewSegmentIndex + 1 < segments.Length
            ? segments[viewSegmentIndex + 1]
            : null;
    }
}
