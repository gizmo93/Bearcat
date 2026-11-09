using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Abstractions.Transfers;
using Bearcat.Hosters.Filer.Api.Requests;
using Bearcat.Hosters.Filer.Api.Responses;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Refit;

namespace Bearcat.Hosters.Filer.Api;

public partial class ApiClient(
    IFilerApi api,
    HttpClientProvider httpClientProvider,
    HosterFileDownloader fileDownloader,
    ILogger<ApiClient> logger
) : IFilerApiClient
{
    public const string ApiBaseUrl = "https://filer.net";

    private const string FileLinkBaseUrl = "https://filer.net/get/";

    private const string SessionCookieName = "jwt";

    private const string MimeType = "application/octet-stream";

    private const int MaxFileHashesPerStatusRequest = 5;

    private const int DownloadRedirectCode = 302;

    private const int FileNotFoundCode = 505;

    private static readonly Uri ApiBaseUri = new(ApiBaseUrl);

    private readonly KeyedAuthTokenCache sessionCookieCache = new(TimeSpan.FromDays(1));

    public async Task<FilerUploadResult> UploadFileAsync(
        FilerConfig config,
        Stream stream,
        string fileName,
        long fileSize,
        string? folderHash,
        CancellationToken cancellationToken
    )
    {
        var uploadServer = await GetUploadServerAsync(config, cancellationToken);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(uploadServer.Session!), "session");
        content.Add(new StringContent(string.Empty), "folder_name");

        content.Add(
            new StringContent(uploadServer.UserId!.Value.ToString(CultureInfo.InvariantCulture)),
            "user_id"
        );

        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(MimeType);
        fileContent.Headers.ContentLength = fileSize;
        content.Add(fileContent, "files", fileName);

        using var httpClient = httpClientProvider.GetUploadClient();
        using var response = await httpClient.PostAsync(
            $"https://{uploadServer.Server}/upload",
            content,
            cancellationToken
        );

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Filer upload failed with status code {response.StatusCode}: {responseContent}"
            );
        }

        var uploadedFile = ReadUploadedFile(responseContent);

        if (folderHash is not null)
        {
            await MoveFileToFolderAsync(
                config: config,
                fileHash: uploadedFile.Hash!,
                editCode: uploadedFile.Edit!,
                folderHash: folderHash,
                cancellationToken: cancellationToken
            );
        }

        return new FilerUploadResult(
            FileHash: uploadedFile.Hash!,
            EditCode: uploadedFile.Edit!,
            FileUrl: $"{FileLinkBaseUrl}{uploadedFile.Hash}"
        );
    }

    public async Task<string> CreateFolderAsync(
        FilerConfig config,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("Creating Filer folder {FolderName}", folderName);

        var response = await SendWithSessionCookieAsync(
            config: config,
            sendRequestAsync: cookie =>
                api.CreateFolderAsync(
                    cookie: cookie,
                    request: new CreateFolderRequest(folderName, ParentPath: string.Empty),
                    cancellationToken: cancellationToken
                ),
            isSessionGone: IsUnauthorized,
            cancellationToken: cancellationToken
        );

        if (
            response is
            {
                IsSuccessStatusCode: true,
                Content: { Success: true, Folder.Hash: { Length: > 0 } folderHash }
            }
        )
        {
            return folderHash;
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return await GetExistingFolderHashAsync(config, folderName, cancellationToken);
        }

        throw new HttpRequestException(
            $"Filer folder creation failed with status code {response.StatusCode}: {ReadErrorMessage(response) ?? response.Content?.Message}"
        );
    }

    public async Task MoveFileToFolderAsync(
        FilerConfig config,
        string fileHash,
        string editCode,
        string folderHash,
        CancellationToken cancellationToken
    )
    {
        var response = await SendWithSessionCookieAsync(
            config: config,
            sendRequestAsync: cookie =>
                api.MoveFilesAsync(
                    cookie: cookie,
                    request: new MoveFilesRequest(
                        FileHashes: [fileHash],
                        TargetFolderHash: folderHash,
                        EditCodePerFileHash: new Dictionary<string, string>
                        {
                            [fileHash] = editCode,
                        }
                    ),
                    cancellationToken: cancellationToken
                ),
            isSessionGone: IsUnauthorized,
            cancellationToken: cancellationToken
        );

        if (!response.IsSuccessStatusCode || response.Content is null)
        {
            throw new HttpRequestException(
                $"Filer file move failed with status code {response.StatusCode}: {ReadErrorMessage(response)}"
            );
        }

        if (response.Content.MovedFiles != 1)
        {
            throw new HttpRequestException(
                $"Filer did not move file {fileHash} into folder {folderHash}: {response.Content.Message}"
            );
        }
    }

    public async Task DownloadFileAsync(
        FilerConfig config,
        string fileHash,
        string targetFilePath,
        ITransferProgress progress,
        long? expectedSizeBytes,
        CancellationToken cancellationToken
    )
    {
        using var response = await api.GetDownloadLinkAsync(
            basicCredentials: CreateBasicCredentials(config),
            fileHash: fileHash,
            cancellationToken: cancellationToken
        );

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new HosterCredentialsRejectedException("Filer rejected the email or password");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var downloadLink = JsonSerializer.Deserialize<DownloadLinkResponse>(responseContent);

        if (
            downloadLink is
            {
                Code: (int)HttpStatusCode.OK or DownloadRedirectCode,
                Data.DownloadUrl: { Length: > 0 } downloadUrl
            }
        )
        {
            await fileDownloader.DownloadToFileAsync(
                downloadUrl: downloadUrl,
                targetFilePath: targetFilePath,
                progress: progress,
                expectedSizeBytes: expectedSizeBytes,
                cancellationToken: cancellationToken
            );

            return;
        }

        var message =
            $"Filer download link request for file {fileHash} failed with status code {response.StatusCode}: {downloadLink?.Status ?? responseContent}";

        if (downloadLink?.Code == FileNotFoundCode)
        {
            throw new HosterFileNotFoundException(message);
        }

        if (IsPremiumRequired(downloadLink))
        {
            throw new HttpRequestException(
                $"Filer download requires a premium account: {downloadLink!.Status}"
            );
        }

        throw new HttpRequestException(message);
    }

    public async Task<IReadOnlyDictionary<string, HostedFileInfo>> GetFileInfoPerFileHashAsync(
        IReadOnlyList<string> fileHashes,
        CancellationToken cancellationToken
    )
    {
        var fileInfoPerFileHash = new Dictionary<string, HostedFileInfo>(
            StringComparer.OrdinalIgnoreCase
        );

        foreach (
            var chunk in fileHashes
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Chunk(MaxFileHashesPerStatusRequest)
        )
        {
            var response = await api.GetMultiStatusAsync(
                string.Join('|', chunk),
                cancellationToken
            );

            if (
                response.StatusCode != HttpStatusCode.OK
                || response.Content is not { Code: 200, FileStatusPerFileHash: not null }
            )
            {
                throw new HttpRequestException(
                    $"Filer multi status request failed with status code {response.StatusCode}: {response.Content?.Status ?? GetErrorContent(response)}"
                );
            }

            var fileStatusPerFileHash = new Dictionary<string, FileStatusResponse>(
                response.Content.FileStatusPerFileHash,
                StringComparer.OrdinalIgnoreCase
            );

            foreach (var fileHash in chunk)
            {
                if (fileStatusPerFileHash.TryGetValue(fileHash, out var fileStatus))
                {
                    fileInfoPerFileHash[fileHash] = new HostedFileInfo(
                        IsOnline: true,
                        FileName: fileStatus.FileName,
                        SizeBytes: fileStatus.FileSize
                    );
                }
                else
                {
                    fileInfoPerFileHash[fileHash] = new HostedFileInfo(
                        IsOnline: false,
                        FileName: null,
                        SizeBytes: null
                    );
                }
            }
        }

        return fileInfoPerFileHash;
    }

    public async Task EnsureCredentialsAcceptedAsync(
        FilerConfig config,
        CancellationToken cancellationToken
    )
    {
        var response = await api.GetProfileAsync(CreateBasicCredentials(config), cancellationToken);

        if (response is { StatusCode: HttpStatusCode.OK, Content.Code: 200 })
        {
            return;
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new HosterCredentialsRejectedException("Filer rejected the email or password");
        }

        throw new HttpRequestException(
            $"Filer profile request failed with status code {response.StatusCode}: {GetErrorContent(response)}",
            inner: null,
            statusCode: response.StatusCode
        );
    }

    private static UploadedFileResponse ReadUploadedFile(string uploadResponseContent)
    {
        var bodyMatch = UploadResponseBodyRegex().Match(uploadResponseContent);

        if (!bodyMatch.Success)
        {
            throw new HttpRequestException(
                $"Filer upload response contained no body: {uploadResponseContent}"
            );
        }

        var uploadedFiles = JsonSerializer.Deserialize<List<UploadedFileResponse>>(
            bodyMatch.Groups["json"].Value
        );

        if (uploadedFiles is not [var uploadedFile])
        {
            throw new HttpRequestException(
                $"Filer upload response did not contain exactly one file: {uploadResponseContent}"
            );
        }

        if (uploadedFile.Error.ValueKind != JsonValueKind.False)
        {
            throw new HttpRequestException(
                $"Filer upload failed with code {uploadedFile.Error.GetRawText()}"
            );
        }

        if (
            string.IsNullOrWhiteSpace(uploadedFile.Hash)
            || string.IsNullOrWhiteSpace(uploadedFile.Edit)
        )
        {
            throw new HttpRequestException(
                $"Filer upload response contained no file hash or edit code: {uploadResponseContent}"
            );
        }

        return uploadedFile;
    }

    private async Task<string> GetExistingFolderHashAsync(
        FilerConfig config,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        var response = await SendWithSessionCookieAsync(
            config: config,
            sendRequestAsync: cookie =>
                api.SearchFoldersAsync(cookie, folderName, cancellationToken),
            isSessionGone: IsUnauthorized,
            cancellationToken: cancellationToken
        );

        if (!response.IsSuccessStatusCode || response.Content is null)
        {
            throw new HttpRequestException(
                $"Filer folder search failed with status code {response.StatusCode}: {ReadErrorMessage(response)}"
            );
        }

        return response
                .Content.Folders?.FirstOrDefault(folder =>
                    string.Equals(folder.Name, folderName, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(folder.Hash)
                )
                ?.Hash
            ?? throw new HttpRequestException(
                $"Filer reported folder {folderName} as existing but the folder search did not find it"
            );
    }

    private async Task<UploadServerResponse> GetUploadServerAsync(
        FilerConfig config,
        CancellationToken cancellationToken
    )
    {
        var response = await SendWithSessionCookieAsync(
            config: config,
            sendRequestAsync: cookie => api.GetUploadServerAsync(cookie, cancellationToken),
            isSessionGone: IsUploadServerSessionGone,
            cancellationToken: cancellationToken
        );

        if (
            !response.IsSuccessStatusCode
            || response.Content is null
            || string.IsNullOrWhiteSpace(response.Content.Server)
            || string.IsNullOrWhiteSpace(response.Content.Session)
        )
        {
            throw new HttpRequestException(
                $"Filer upload server request failed with status code {response.StatusCode}: {GetErrorContent(response)}"
            );
        }

        if (response.Content.UserId is null)
        {
            throw new HttpRequestException(
                "Filer upload server session is not logged in although the login succeeded"
            );
        }

        return response.Content;
    }

    private Task<ApiResponse<TContent>> SendWithSessionCookieAsync<TContent>(
        FilerConfig config,
        Func<string, Task<ApiResponse<TContent>>> sendRequestAsync,
        Func<ApiResponse<TContent>, bool> isSessionGone,
        CancellationToken cancellationToken
    )
    {
        return sessionCookieCache.SendWithAuthTokenAsync(
            accountKey: config.Email,
            authenticateAsync: ct => LoginAsync(config, ct),
            sendRequestAsync: sessionCookie => sendRequestAsync(CreateCookieHeader(sessionCookie)),
            isAuthTokenRejected: isSessionGone,
            cancellationToken: cancellationToken
        );
    }

    private static bool IsUploadServerSessionGone(ApiResponse<UploadServerResponse> response)
    {
        return IsUnauthorized(response)
            || response is { IsSuccessStatusCode: true, Content.UserId: null };
    }

    private static bool IsPremiumRequired(DownloadLinkResponse? downloadLink)
    {
        return downloadLink
                is {
                    Code: (int)HttpStatusCode.Forbidden or (int)HttpStatusCode.ServiceUnavailable,
                    Status: { } status,
                }
            && status.Contains("premium", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnauthorized(IApiResponse response)
    {
        return response.StatusCode == HttpStatusCode.Unauthorized;
    }

    private static string CreateBasicCredentials(FilerConfig config)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.Email}:{config.Password}"));
    }

    private async Task<string> LoginAsync(FilerConfig config, CancellationToken cancellationToken)
    {
        logger.LogInformation("Logging in to Filer for account {Email}", config.Email);

        var csrfResponse = await api.GetCsrfTokenAsync(cancellationToken);

        if (
            !csrfResponse.IsSuccessStatusCode
            || string.IsNullOrWhiteSpace(csrfResponse.Content?.CsrfToken)
        )
        {
            throw new HttpRequestException(
                $"Filer csrf token request failed with status code {csrfResponse.StatusCode}: {GetErrorContent(csrfResponse)}"
            );
        }

        var anonymousSessionCookie = ReadSessionCookie(csrfResponse, "Filer csrf token request");

        var loginResponse = await api.LoginAsync(
            csrfToken: csrfResponse.Content.CsrfToken,
            cookie: CreateCookieHeader(anonymousSessionCookie),
            request: new LoginRequest(config.Email, config.Password),
            cancellationToken: cancellationToken
        );

        if (loginResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new HosterCredentialsRejectedException(
                $"Filer rejected the login: {ReadErrorMessage(loginResponse)}"
            );
        }

        if (!loginResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Filer login failed with status code {loginResponse.StatusCode}: {ReadErrorMessage(loginResponse)}",
                inner: null,
                statusCode: loginResponse.StatusCode
            );
        }

        return ReadSessionCookie(loginResponse, "Filer login");
    }

    private static string ReadSessionCookie(IApiResponse response, string operationName)
    {
        var cookieContainer = new CookieContainer();

        if (
            response.Headers is { } headers
            && headers.TryGetValues("Set-Cookie", out var setCookieHeaders)
        )
        {
            foreach (var setCookieHeader in setCookieHeaders)
            {
                cookieContainer.SetCookies(ApiBaseUri, setCookieHeader);
            }
        }

        return cookieContainer.GetCookies(ApiBaseUri)[SessionCookieName]?.Value
            ?? throw new HttpRequestException($"{operationName} returned no session cookie");
    }

    private static string CreateCookieHeader(string sessionCookie)
    {
        return $"{SessionCookieName}={sessionCookie}";
    }

    private static string? ReadErrorMessage(IApiResponse response)
    {
        var content = GetErrorContent(response);

        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<MessageResponse>(content)?.Message ?? content;
        }
        catch (JsonException)
        {
            return content;
        }
    }

    private static string? GetErrorContent(IApiResponse response)
    {
        return (response.Error as ApiException)?.Content;
    }

    [GeneratedRegex("<body>(?<json>.*)</body>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex UploadResponseBodyRegex();
}
