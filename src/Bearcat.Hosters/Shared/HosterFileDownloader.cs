using Bearcat.Abstractions.Hoster;

namespace Bearcat.Hosters.Shared;

public class HosterFileDownloader(HttpClientProvider httpClientProvider)
{
    private const string PartFileExtension = ".part";

    private const int CopyBufferSizeBytes = 81920;

    public async Task DownloadToFileAsync(
        string downloadUrl,
        string targetFilePath,
        IDownloadProgress progress,
        long? expectedSizeBytes,
        CancellationToken cancellationToken
    )
    {
        var partFilePath = targetFilePath + PartFileExtension;

        using var httpClient = httpClientProvider.GetDownloadClient();

        try
        {
            using var response = await httpClient.GetAsync(
                downloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Download request failed with status code {(int)response.StatusCode} ({response.StatusCode})"
                );
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(
                cancellationToken
            );
            await using var countingStream = new CountingDownloadStream(
                inner: responseStream,
                progress: progress,
                totalBytes: response.Content.Headers.ContentLength ?? expectedSizeBytes
            );
            await using var fileStream = new FileStream(
                path: partFilePath,
                mode: FileMode.Create,
                access: FileAccess.Write,
                share: FileShare.None,
                bufferSize: CopyBufferSizeBytes,
                useAsync: true
            );

            await countingStream.CopyToAsync(fileStream, CopyBufferSizeBytes, cancellationToken);
            await fileStream.FlushAsync(cancellationToken);
        }
        catch
        {
            DeleteIfExists(partFilePath);
            throw;
        }

        File.Move(sourceFileName: partFilePath, destFileName: targetFilePath, overwrite: true);
    }

    private static void DeleteIfExists(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}
