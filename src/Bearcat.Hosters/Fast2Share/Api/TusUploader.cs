using System.Buffers;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Fast2Share.Api;

public sealed class TusUploader(HttpClientProvider httpClientProvider, ILogger logger)
{
    private const string TusVersion = "1.0.0";

    private const string TusResumableHeader = "Tus-Resumable";

    private const string UploadOffsetHeader = "Upload-Offset";

    private const string UploadLengthHeader = "Upload-Length";

    private const string UploadMetadataHeader = "Upload-Metadata";

    private const int ChunkRetryAttempts = 3;

    public int ChunkSize { get; set; } = 8 * 1024 * 1024;

    public TimeSpan ChunkRetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    public async Task UploadAsync(
        string uploadUrl,
        string uploadToken,
        string metadataKey,
        Stream stream,
        long fileSize,
        CancellationToken cancellationToken
    )
    {
        using var httpClient = httpClientProvider.GetUploadClient();

        var location = await CreateUploadAsync(
            httpClient: httpClient,
            uploadUrl: uploadUrl,
            uploadToken: uploadToken,
            metadataKey: metadataKey,
            fileSize: fileSize,
            cancellationToken: cancellationToken
        );

        var buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);

        try
        {
            var offset = 0L;

            while (offset < fileSize)
            {
                var chunkLength = (int)Math.Min(ChunkSize, fileSize - offset);
                await stream.ReadExactlyAsync(buffer.AsMemory(0, chunkLength), cancellationToken);

                offset = await SendChunkAsync(
                    httpClient: httpClient,
                    location: location,
                    buffer: buffer,
                    chunkLength: chunkLength,
                    offset: offset,
                    cancellationToken: cancellationToken
                );
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task<Uri> CreateUploadAsync(
        HttpClient httpClient,
        string uploadUrl,
        string uploadToken,
        string metadataKey,
        long fileSize,
        CancellationToken cancellationToken
    )
    {
        var encodedToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(uploadToken));

        using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        request.Headers.Add(TusResumableHeader, TusVersion);
        request.Headers.Add(UploadLengthHeader, fileSize.ToString(CultureInfo.InvariantCulture));
        request.Headers.Add(UploadMetadataHeader, $"{metadataKey} {encodedToken}");
        request.Content = new ByteArrayContent([]);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new HttpRequestException(
                $"Fast2Share tus upload creation failed with status code {response.StatusCode}: {content}"
            );
        }

        var location =
            response.Headers.Location
            ?? throw new HttpRequestException(
                "Fast2Share tus upload creation returned no Location header"
            );

        return location.IsAbsoluteUri ? location : new Uri(new Uri(uploadUrl), location);
    }

    private async Task<long> SendChunkAsync(
        HttpClient httpClient,
        Uri location,
        byte[] buffer,
        int chunkLength,
        long offset,
        CancellationToken cancellationToken
    )
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await PatchChunkAsync(
                    httpClient: httpClient,
                    location: location,
                    buffer: buffer,
                    chunkLength: chunkLength,
                    offset: offset,
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception ex)
                when (attempt < ChunkRetryAttempts && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    ex,
                    "Fast2Share tus chunk at offset {Offset} failed on attempt {Attempt}, resuming: {ErrorMessage}",
                    offset,
                    attempt,
                    ex.InnerException?.Message ?? ex.Message
                );

                await Task.Delay(ChunkRetryDelay, cancellationToken);

                var serverOffset = await GetOffsetAsync(httpClient, location, cancellationToken);

                if (serverOffset == offset + chunkLength)
                {
                    return serverOffset;
                }

                if (serverOffset != offset)
                {
                    throw new HttpRequestException(
                        $"Fast2Share tus upload cannot be resumed: server is at offset {serverOffset} but the current chunk starts at {offset}"
                    );
                }
            }
        }
    }

    private static async Task<long> PatchChunkAsync(
        HttpClient httpClient,
        Uri location,
        byte[] buffer,
        int chunkLength,
        long offset,
        CancellationToken cancellationToken
    )
    {
        using var content = new ByteArrayContent(buffer, 0, chunkLength);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");

        using var request = new HttpRequestMessage(HttpMethod.Patch, location)
        {
            Content = content,
        };
        request.Headers.Add(TusResumableHeader, TusVersion);
        request.Headers.Add(UploadOffsetHeader, offset.ToString(CultureInfo.InvariantCulture));

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new HttpRequestException(
                $"Fast2Share tus chunk upload at offset {offset} failed with status code {response.StatusCode}: {responseContent}"
            );
        }

        var newOffset = ReadOffsetHeader(response) ?? offset + chunkLength;

        if (newOffset != offset + chunkLength)
        {
            throw new HttpRequestException(
                $"Fast2Share tus chunk upload at offset {offset} was acknowledged with unexpected offset {newOffset}"
            );
        }

        return newOffset;
    }

    private static async Task<long> GetOffsetAsync(
        HttpClient httpClient,
        Uri location,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, location);
        request.Headers.Add(TusResumableHeader, TusVersion);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Fast2Share tus offset request failed with status code {response.StatusCode}"
            );
        }

        return ReadOffsetHeader(response)
            ?? throw new HttpRequestException(
                "Fast2Share tus offset request returned no Upload-Offset header"
            );
    }

    private static long? ReadOffsetHeader(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues(UploadOffsetHeader, out var values))
        {
            return null;
        }

        return long.TryParse(
            values.FirstOrDefault(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var offset
        )
            ? offset
            : null;
    }
}
