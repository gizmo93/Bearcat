using System.Net;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Transfers;

namespace Bearcat.Hosters.Shared;

public class HosterFileDownloader(HttpClientProvider httpClientProvider)
{
    private const string PartFileExtension = ".part";

    private const int CopyBufferSizeBytes = 81920;

    private const int MaxErrorBodyLength = 500;

    public async Task DownloadToFileAsync(
        string downloadUrl,
        string targetFilePath,
        ITransferProgress progress,
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
                throw await CreateRequestFailedExceptionAsync(
                    downloadUrl: downloadUrl,
                    response: response,
                    cancellationToken: cancellationToken
                );
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(
                cancellationToken
            );
            await using var progressStream = new ProgressReportingStream(
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

            await progressStream.CopyToAsync(fileStream, CopyBufferSizeBytes, cancellationToken);
            await fileStream.FlushAsync(cancellationToken);
        }
        catch
        {
            DeleteIfExists(partFilePath);
            throw;
        }

        File.Move(sourceFileName: partFilePath, destFileName: targetFilePath, overwrite: true);
    }

    private static async Task<Exception> CreateRequestFailedExceptionAsync(
        string downloadUrl,
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        var requestUrl = new Uri(downloadUrl).GetLeftPart(UriPartial.Path);
        var bodySnippet = await ReadBodySnippetAsync(response, cancellationToken);

        var message =
            $"Download request for {requestUrl} failed with status code {(int)response.StatusCode} ({response.StatusCode})";

        if (bodySnippet.Length > 0)
        {
            message += $": {bodySnippet}";
        }

        return response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone
            ? new HosterFileNotFoundException(message)
            : new HttpRequestException(
                message: message,
                inner: null,
                statusCode: response.StatusCode
            );
    }

    private static async Task<string> ReadBodySnippetAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            var collapsed = string.Join(
                ' ',
                body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            );

            return collapsed.Length > MaxErrorBodyLength
                ? collapsed[..MaxErrorBodyLength]
                : collapsed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return string.Empty;
        }
    }

    private static void DeleteIfExists(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}
