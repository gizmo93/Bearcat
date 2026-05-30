using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Hosters.Fichier.Api.File;
using Bearcat.Hosters.Fichier.Api.Folder;
using Bearcat.Hosters.Fichier.Api.Upload;
using Bearcat.Hosters.Fichier.Api.User;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Refit;

namespace Bearcat.Hosters.Fichier.Api;

public class ApiClient(
    IFichierApi api,
    HttpClientProvider httpClientProvider,
    ILogger<ApiClient> logger
) : IFichierApiClient
{
    public const string ApiBaseUrl = "https://api.1fichier.com/v1";

    public const string UploadHttpClientName = "FichierUploadHttpClient";

    private const int MaxParallelLinkChecks = 3;

    private const int MaxLinkCheckAttempts = 3;

    private const int RootFolderId = 0;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
    };

    public TimeSpan RateLimitRetryDelay { get; init; } = TimeSpan.FromSeconds(5);

    public async Task<EndUploadResponse> UploadFileAsync(
        FichierConfig config,
        Stream stream,
        string fileName,
        string? folderId,
        CancellationToken cancellationToken
    )
    {
        var uploadServer = await GetUploadServerAsync(config, cancellationToken);

        if (!IsValidUploadId(uploadServer.Id) || string.IsNullOrWhiteSpace(uploadServer.Url))
        {
            throw new HttpRequestException("1fichier did not return a valid upload server");
        }

        await UploadToServerAsync(
            config,
            uploadServer,
            stream,
            fileName,
            folderId,
            cancellationToken
        );

        var endUploadResponse = await EndUploadAsync(uploadServer, cancellationToken);

        if (endUploadResponse.Links.Count == 0)
        {
            throw new HttpRequestException(
                endUploadResponse.Message
                    ?? endUploadResponse.Status
                    ?? $"1fichier did not return a download link. Response: {endUploadResponse.RawContent}"
            );
        }

        return endUploadResponse;
    }

    public async Task<string> CreateFolderAsync(
        FichierConfig config,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        var authorization = GetAuthorizationHeader(config.ApiKey);
        var rootFolder = await api.GetFolderListAsync(
            authorization,
            new FolderListRequest { FolderId = RootFolderId },
            cancellationToken
        );

        EnsureOk(rootFolder.Status, rootFolder.Message, "1fichier folder list failed");

        var existingFolder = rootFolder.SubFolders.FirstOrDefault(folder =>
            string.Equals(folder.Name, folderName, StringComparison.Ordinal)
            && folder.Id is not null
        );

        if (existingFolder is not null)
        {
            return existingFolder.Id!.Value.ToString();
        }

        var createdFolder = await api.CreateFolderAsync(
            authorization,
            new CreateFolderRequest { Name = folderName, FolderId = RootFolderId },
            cancellationToken
        );

        EnsureOk(createdFolder.Status, createdFolder.Message, "1fichier folder creation failed");

        return createdFolder.FolderId?.ToString()
            ?? throw new HttpRequestException("1fichier folder creation returned no folder id");
    }

    public async Task<IReadOnlyDictionary<string, bool>> CheckLinksAsync(
        FichierConfig config,
        IReadOnlyList<string> fileUrls,
        CancellationToken cancellationToken
    )
    {
        using var semaphore = new SemaphoreSlim(MaxParallelLinkChecks);

        var checkTasks = fileUrls
            .Distinct()
            .Select(fileUrl =>
                CheckLinkAsync(
                    config: config,
                    fileUrl: fileUrl,
                    semaphore: semaphore,
                    cancellationToken: cancellationToken
                )
            )
            .ToList();

        var results = await Task.WhenAll(checkTasks);

        return results.ToDictionary(result => result.FileUrl, result => result.IsOnline);
    }

    public async Task<UserInfoResponse> GetUserInfoAsync(
        FichierConfig config,
        CancellationToken cancellationToken
    )
    {
        var response = await api.GetUserInfoAsync(
            GetAuthorizationHeader(config.ApiKey),
            new UserInfoRequest(),
            cancellationToken
        );

        if (!response.IsSuccessStatusCode || response.Content is null)
        {
            throw new HttpRequestException(
                response.Content?.Message
                    ?? $"1fichier user info request failed with status code {response.StatusCode}"
            );
        }

        return response.Content;
    }

    private async Task UploadToServerAsync(
        FichierConfig config,
        GetUploadServerResponse uploadServer,
        Stream stream,
        string fileName,
        string? folderId,
        CancellationToken cancellationToken
    )
    {
        using var httpClient = httpClientProvider.GetClient(UploadHttpClientName);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://{uploadServer.Url}/upload.cgi?id={Uri.EscapeDataString(uploadServer.Id)}"
        );

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        request.Content = new FichierUploadContent(
            stream: stream,
            fileName: fileName,
            folderId: ParseFolderId(folderId)
        );

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );

        if ((int)response.StatusCode is >= 300 and < 400)
        {
            var location = response.Headers.Location?.ToString();

            if (
                string.IsNullOrWhiteSpace(location)
                || !location.Contains(
                    $"/end.pl?xid={uploadServer.Id}",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                throw new HttpRequestException(
                    $"1fichier upload returned redirect without expected end.pl location. Status code: {response.StatusCode}, Location: {location}"
                );
            }

            return;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        throw new HttpRequestException(
            $"1fichier upload failed with status code {(int)response.StatusCode} ({response.StatusCode}): {content}"
        );
    }

    private async Task<GetUploadServerResponse> GetUploadServerAsync(
        FichierConfig config,
        CancellationToken cancellationToken
    )
    {
        using var httpClient = httpClientProvider.GetClient(UploadHttpClientName);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{ApiBaseUrl}/upload/get_upload_server.cgi"
        );

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"1fichier upload server request failed with status code {response.StatusCode}: {content}"
            );
        }

        return JsonSerializer.Deserialize<GetUploadServerResponse>(content, JsonSerializerOptions)
            ?? throw new HttpRequestException("1fichier returned an empty upload server response");
    }

    private async Task<EndUploadResponse> EndUploadAsync(
        GetUploadServerResponse uploadServer,
        CancellationToken cancellationToken
    )
    {
        using var httpClient = httpClientProvider.GetClient(UploadHttpClientName);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://{uploadServer.Url}/end.pl?xid={Uri.EscapeDataString(uploadServer.Id)}"
        );

        request.Headers.Add("JSON", "1");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        var endUploadResponse =
            JsonSerializer.Deserialize<EndUploadResponse>(content, JsonSerializerOptions)
            ?? throw new HttpRequestException("1fichier returned an empty upload result");

        endUploadResponse.RawContent = content;

        return endUploadResponse;
    }

    private async Task<(string FileUrl, bool IsOnline)> CheckLinkAsync(
        FichierConfig config,
        string fileUrl,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken
    )
    {
        foreach (var attempt in Enumerable.Range(1, MaxLinkCheckAttempts))
        {
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                var response = await api.GetFileInfoAsync(
                    GetAuthorizationHeader(config.ApiKey),
                    new FileInfoRequest { Url = fileUrl },
                    cancellationToken
                );

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return (fileUrl, false);
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    logger.LogInformation(
                        "Rate limited by 1fichier API while checking {FileUrl}, waiting before retrying (Attempt {Attempt})",
                        fileUrl,
                        attempt
                    );
                }
                else
                {
                    return (
                        fileUrl,
                        response.IsSuccessStatusCode
                            && response.Content is { Url: not null }
                            && !string.Equals(
                                response.Content.Status,
                                "KO",
                                StringComparison.OrdinalIgnoreCase
                            )
                    );
                }
            }
            catch (ApiException exception)
                when (exception.StatusCode == HttpStatusCode.TooManyRequests)
            {
                logger.LogInformation(
                    "Rate limited by 1fichier API while checking {FileUrl}, waiting before retrying (Attempt {Attempt})",
                    fileUrl,
                    attempt
                );
            }
            catch (ApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                return (fileUrl, false);
            }
            finally
            {
                semaphore.Release();
            }

            if (attempt < MaxLinkCheckAttempts)
            {
                await Task.Delay(RateLimitRetryDelay, cancellationToken);
            }
        }

        return (fileUrl, false);
    }

    private static string GetAuthorizationHeader(string apiKey)
    {
        return $"Bearer {apiKey}";
    }

    private static int ParseFolderId(string? folderId)
    {
        if (string.IsNullOrWhiteSpace(folderId))
        {
            return RootFolderId;
        }

        return int.TryParse(folderId, out var parsedFolderId)
            ? parsedFolderId
            : throw new ArgumentException(
                $"Invalid 1fichier folder id: {folderId}",
                nameof(folderId)
            );
    }

    private static void EnsureOk(string? status, string? message, string errorPrefix)
    {
        if (!string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
        {
            throw new HttpRequestException(
                $"{errorPrefix}: {message ?? status ?? "unknown error"}"
            );
        }
    }

    private static bool IsValidUploadId(string uploadId)
    {
        return !string.IsNullOrWhiteSpace(uploadId)
            && uploadId.Length <= 10
            && uploadId.All(char.IsLetterOrDigit);
    }

    private sealed class FichierUploadContent : HttpContent
    {
        private readonly Stream stream;
        private readonly string boundary;
        private readonly byte[] prefixBytes;
        private readonly byte[] suffixBytes;

        public FichierUploadContent(Stream stream, string fileName, int folderId)
        {
            this.stream = stream;
            boundary = "------------------------" + Guid.NewGuid().ToString("N")[..16];

            Headers.ContentType = MediaTypeHeaderValue.Parse(
                $"multipart/form-data; boundary={boundary}"
            );

            prefixBytes = Encoding.ASCII.GetBytes(
                $"--{boundary}\r\n"
                    + "Content-Disposition: form-data; name=\"did\"\r\n"
                    + "\r\n"
                    + $"{folderId}\r\n"
                    + $"--{boundary}\r\n"
                    + $"Content-Disposition: form-data; name=\"file[]\"; filename=\"{EscapeQuotedString(fileName)}\"\r\n"
                    + "\r\n"
            );

            suffixBytes = Encoding.ASCII.GetBytes($"\r\n--{boundary}--\r\n");
        }

        protected override async Task SerializeToStreamAsync(
            Stream targetStream,
            TransportContext? context
        )
        {
            await targetStream.WriteAsync(prefixBytes);
            await stream.CopyToAsync(targetStream);
            await targetStream.WriteAsync(suffixBytes);
        }

        protected override bool TryComputeLength(out long length)
        {
            if (!stream.CanSeek)
            {
                length = 0;
                return false;
            }

            length = prefixBytes.Length + (stream.Length - stream.Position) + suffixBytes.Length;
            return true;
        }

        private static string EscapeQuotedString(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
