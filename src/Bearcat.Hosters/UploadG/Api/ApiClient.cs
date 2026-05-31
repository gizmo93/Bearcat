using System.Buffers;
using System.Net;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.UploadG.Api;

public class ApiClient(
    IUploadGApi api,
    HttpClientProvider httpClientProvider,
    ILogger<ApiClient> logger
) : IUploadGApiClient
{
    public const string ApiBaseUrl = "https://uploadg.com/api/v1";

    private const string PublicBaseUrl = "https://uploadg.com";

    private const int ChunkSize = 50 * 1024 * 1024;

    private const int MaxParallelLinkChecks = 5;

    public TimeSpan FastApiRequestTimeout { get; set; } = TimeSpan.FromMinutes(2);

    public async Task<UploadFileResponse> UploadFileAsync(
        UploadGConfig config,
        Stream stream,
        string fileName,
        string? folderId,
        long fileSize,
        CancellationToken cancellationToken
    )
    {
        var authorization = GetAuthorizationHeader(config.ApiKey);
        var extension = Path.GetExtension(fileName).TrimStart('.');

        var multipartUpload = await ExecuteFastApiRequestAsync(
            operationName: "multipart upload creation",
            action: token =>
                api.CreateMultipartUploadAsync(
                    authorization: authorization,
                    request: new MultipartCreateRequest(
                        Filename: Path.GetFileNameWithoutExtension(fileName),
                        Mime: "application/octet-stream",
                        Size: fileSize,
                        Extension: extension
                    ),
                    cancellationToken: token
                ),
            cancellationToken: cancellationToken
        );

        if (
            !IsSuccess(multipartUpload.Status)
            || string.IsNullOrWhiteSpace(multipartUpload.Key)
            || string.IsNullOrWhiteSpace(multipartUpload.UploadId)
            || string.IsNullOrWhiteSpace(multipartUpload.StorageBucket)
        )
        {
            throw new HttpRequestException("UploadG multipart upload creation failed");
        }

        var parts = await UploadPartsAsync(
            authorization: authorization,
            stream: stream,
            multipartUpload: multipartUpload,
            fileSize: fileSize,
            cancellationToken: cancellationToken
        );

        var completeResponse = await ExecuteFastApiRequestAsync(
            operationName: "multipart upload completion",
            action: token =>
                api.CompleteMultipartUploadAsync(
                    authorization: authorization,
                    request: new MultipartCompleteRequest(
                        Key: multipartUpload.Key,
                        UploadId: multipartUpload.UploadId,
                        StorageBucket: multipartUpload.StorageBucket,
                        Parts: parts
                    ),
                    cancellationToken: token
                ),
            cancellationToken: cancellationToken
        );

        if (!IsSuccess(completeResponse.Status))
        {
            throw new HttpRequestException("UploadG multipart upload completion failed");
        }

        var response = await ExecuteFastApiRequestAsync(
            operationName: "file entry creation",
            action: token =>
                api.CreateS3EntryAsync(
                    authorization: authorization,
                    request: new CreateS3EntryRequest(
                        ClientName: fileName,
                        ClientExtension: extension,
                        ClientMime: "application/octet-stream",
                        Filename: multipartUpload.Key.Split('/').Last(),
                        Size: fileSize,
                        ParentId: TryParseId(folderId),
                        StorageBucket: multipartUpload.StorageBucket
                    ),
                    cancellationToken: token
                ),
            cancellationToken: cancellationToken
        );

        if (!IsSuccess(response.Status) || response.FileEntry is null)
        {
            throw new HttpRequestException("UploadG file entry creation failed");
        }

        return response;
    }

    public async Task<string> CreateFolderAsync(
        UploadGConfig config,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        var authorization = GetAuthorizationHeader(config.ApiKey);
        logger.LogInformation("Checking UploadG folder {FolderName}", folderName);
        var existingFolderId = await GetExistingFolderIdAsync(
            authorization: authorization,
            folderName: folderName,
            cancellationToken: cancellationToken
        );

        if (existingFolderId is not null)
        {
            return existingFolderId.Value.ToString();
        }

        logger.LogInformation("Creating UploadG folder {FolderName}", folderName);
        var response = await ExecuteFastApiRequestAsync(
            operationName: "folder creation",
            action: token =>
                api.CreateFolderAsync(
                    authorization,
                    new CreateFolderRequest(folderName, ParentId: null),
                    token
                ),
            cancellationToken: cancellationToken
        );

        if (!IsSuccess(response.Status) || response.Folder is null)
        {
            throw new HttpRequestException("UploadG folder creation failed");
        }

        return response.Folder.Id.ToString();
    }

    public async Task<IReadOnlyDictionary<string, bool>> CheckLinksAsync(
        UploadGConfig config,
        IReadOnlyList<FileUrlToCheckDto> files,
        CancellationToken cancellationToken
    )
    {
        var authorization = GetAuthorizationHeader(config.ApiKey);
        using var semaphore = new SemaphoreSlim(MaxParallelLinkChecks);

        var tasks = files
            .Distinct()
            .Select(file =>
                CheckLinkAsync(
                    authorization: authorization,
                    file: file,
                    semaphore: semaphore,
                    cancellationToken: cancellationToken
                )
            )
            .ToList();

        var results = await Task.WhenAll(tasks);

        return results.ToDictionary(result => result.FileUrl, result => result.IsOnline);
    }

    public async Task<bool> IsApiKeyValidAsync(
        UploadGConfig config,
        CancellationToken cancellationToken
    )
    {
        var response = await ExecuteFastApiRequestAsync(
            operationName: "space usage request",
            action: token => api.GetSpaceUsageAsync(GetAuthorizationHeader(config.ApiKey), token),
            cancellationToken: cancellationToken
        );

        return response.StatusCode == HttpStatusCode.OK;
    }

    public async Task<string> GetOrCreateShareableLinkAsync(
        UploadGConfig config,
        long entryId,
        CancellationToken cancellationToken
    )
    {
        var authorization = GetAuthorizationHeader(config.ApiKey);

        var existingResponse = await ExecuteFastApiRequestAsync(
            operationName: "shareable link lookup",
            action: token => api.GetShareableLinkAsync(authorization, entryId, token),
            cancellationToken: cancellationToken
        );

        if (
            existingResponse.StatusCode == HttpStatusCode.OK
            && !string.IsNullOrWhiteSpace(existingResponse.Content?.Link?.Hash)
        )
        {
            return BuildShareableUrl(existingResponse.Content.Link.Hash);
        }

        var createResponse = await ExecuteFastApiRequestAsync(
            operationName: "shareable link creation",
            action: token =>
                api.CreateShareableLinkAsync(
                    authorization,
                    entryId,
                    new CreateShareableLinkRequest(AllowDownload: true, AllowEdit: false),
                    token
                ),
            cancellationToken: cancellationToken
        );

        if (
            !IsSuccess(createResponse.Status)
            || string.IsNullOrWhiteSpace(createResponse.Link?.Hash)
        )
        {
            throw new HttpRequestException("UploadG shareable link creation failed");
        }

        return BuildShareableUrl(createResponse.Link.Hash);
    }

    private async Task<IReadOnlyList<UploadedPart>> UploadPartsAsync(
        string authorization,
        Stream stream,
        MultipartCreateResponse multipartUpload,
        long fileSize,
        CancellationToken cancellationToken
    )
    {
        var parts = new List<UploadedPart>();
        var uploadedBytes = 0L;

        for (var partNumber = 1; uploadedBytes < fileSize; partNumber++)
        {
            var partSize = Math.Min(ChunkSize, fileSize - uploadedBytes);

            var signResponse = await ExecuteFastApiRequestAsync(
                operationName: $"signed URL request for part {partNumber}",
                action: token =>
                    api.SignPartUrlsAsync(
                        authorization: authorization,
                        request: new BatchSignPartUrlsRequest(
                            PartNumbers: [partNumber],
                            UploadId: multipartUpload.UploadId!,
                            Key: multipartUpload.Key!,
                            StorageBucket: multipartUpload.StorageBucket!
                        ),
                        cancellationToken: token
                    ),
                cancellationToken: cancellationToken
            );

            var signedUrl = signResponse.Urls?.FirstOrDefault(url =>
                url.PartNumber == partNumber && !string.IsNullOrWhiteSpace(url.Url)
            );

            if (!IsSuccess(signResponse.Status) || signedUrl?.Url is null)
            {
                throw new HttpRequestException(
                    $"UploadG signed URL request failed for part {partNumber}"
                );
            }

            var etag = await UploadPartAsync(signedUrl.Url, stream, partSize, cancellationToken);

            parts.Add(new UploadedPart(etag, partNumber));
            uploadedBytes += partSize;
        }

        if (parts.Count == 0)
        {
            throw new HttpRequestException("UploadG multipart upload produced no parts");
        }

        return parts;
    }

    private async Task<string> UploadPartAsync(
        string signedUrl,
        Stream stream,
        long partSize,
        CancellationToken cancellationToken
    )
    {
        using var httpClient = httpClientProvider.GetUploadClient();
        using var content = new StreamPartContent(stream, partSize);
        content.Headers.ContentType = null;

        using var request = new HttpRequestMessage(HttpMethod.Put, signedUrl);
        request.Content = content;

        var response = await httpClient.SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"UploadG part upload failed with status code {response.StatusCode}: {responseContent}"
            );
        }

        if (!string.IsNullOrWhiteSpace(response.Headers.ETag?.Tag))
        {
            return response.Headers.ETag.Tag;
        }

        if (
            response.Headers.TryGetValues("ETag", out var values)
            && values.FirstOrDefault() is { } etag
            && !string.IsNullOrWhiteSpace(etag)
        )
        {
            return etag;
        }

        throw new HttpRequestException("UploadG part upload response did not include an ETag");
    }

    private async Task<(string FileUrl, bool IsOnline)> CheckLinkAsync(
        string authorization,
        FileUrlToCheckDto file,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken
    )
    {
        var fileUrl = file.Url;

        if (!IsUploadGShareableLink(fileUrl))
        {
            return (FileUrl: fileUrl, IsOnline: false);
        }

        await semaphore.WaitAsync(cancellationToken);

        try
        {
            var entryId = TryParseId(file.ExternalId);

            if (entryId is not null)
            {
                var shareableLinkResponse = await ExecuteFastApiRequestAsync(
                    operationName: "shareable link lookup",
                    action: token => api.GetShareableLinkAsync(authorization, entryId.Value, token),
                    cancellationToken: cancellationToken
                );

                return (
                    FileUrl: fileUrl,
                    IsOnline: shareableLinkResponse.StatusCode == HttpStatusCode.OK
                        && !string.IsNullOrWhiteSpace(shareableLinkResponse.Content?.Link?.Hash)
                );
            }

            using var httpClient = httpClientProvider.GetUploadClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, fileUrl);
            var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );

            return (FileUrl: fileUrl, IsOnline: response.StatusCode == HttpStatusCode.OK);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Failed to check UploadG link {FileUrl}: {Message}",
                fileUrl,
                ex.InnerException?.Message ?? ex.Message
            );

            return (FileUrl: fileUrl, IsOnline: false);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<long?> GetExistingFolderIdAsync(
        string authorization,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        var fileEntries = await ExecuteFastApiRequestAsync(
            operationName: "folder lookup",
            action: token =>
                api.ListFileEntriesAsync(
                    authorization,
                    perPage: 50,
                    type: "folder",
                    query: folderName,
                    parentIds: "0",
                    token
                ),
            cancellationToken: cancellationToken
        );

        return fileEntries
            .Data.FirstOrDefault(folder =>
                string.Equals(folder.Type, "folder", StringComparison.OrdinalIgnoreCase)
                && string.Equals(folder.Name, folderName, StringComparison.Ordinal)
                && (folder.ParentId is null or 0)
            )
            ?.Id;
    }

    private async Task<T> ExecuteFastApiRequestAsync<T>(
        string operationName,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken
    )
    {
        using var timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken
        );

        if (FastApiRequestTimeout != Timeout.InfiniteTimeSpan)
        {
            timeoutCancellationTokenSource.CancelAfter(FastApiRequestTimeout);
        }

        try
        {
            return await action(timeoutCancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
            when (timeoutCancellationTokenSource.IsCancellationRequested
                && !cancellationToken.IsCancellationRequested
            )
        {
            throw new TimeoutException(
                $"UploadG {operationName} timed out after {FastApiRequestTimeout.Seconds} seconds"
            );
        }
    }

    private static long? TryParseId(string? value)
    {
        return long.TryParse(value, out var id) ? id : null;
    }

    private static bool IsUploadGShareableLink(string fileUrl)
    {
        if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Host, "uploadg.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = uri
            .AbsolutePath.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
            .ToList();

        var shareSegmentIndex = segments.FindIndex(segment =>
            string.Equals(segment, "s", StringComparison.OrdinalIgnoreCase)
        );

        return shareSegmentIndex >= 0 && segments.Count > shareSegmentIndex + 1;
    }

    private static string BuildShareableUrl(string hash)
    {
        return $"{PublicBaseUrl}/drive/s/{hash}";
    }

    private static string GetAuthorizationHeader(string apiKey)
    {
        return $"Bearer {apiKey}";
    }

    private static bool IsSuccess(string? status)
    {
        return string.Equals(status, "success", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StreamPartContent(Stream source, long length) : HttpContent
    {
        protected override bool TryComputeLength(out long computedLength)
        {
            computedLength = length;

            return true;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            return SerializeToStreamAsync(stream, context, CancellationToken.None);
        }

        protected override async Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context,
            CancellationToken cancellationToken
        )
        {
            var buffer = ArrayPool<byte>.Shared.Rent(81920);
            var remainingBytes = length;

            try
            {
                while (remainingBytes > 0)
                {
                    var bytesToRead = (int)Math.Min(buffer.Length, remainingBytes);
                    var bytesRead = await source.ReadAsync(
                        buffer.AsMemory(0, bytesToRead),
                        cancellationToken
                    );

                    if (bytesRead == 0)
                    {
                        throw new EndOfStreamException(
                            "UploadG file stream ended before the current part was fully read"
                        );
                    }

                    await stream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);

                    remainingBytes -= bytesRead;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
