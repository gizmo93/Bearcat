using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Hosters.Extensions;
using Bearcat.Hosters.Rapidgator.Api.File;
using Bearcat.Hosters.Rapidgator.Api.Folder;
using Bearcat.Hosters.Rapidgator.Api.User;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Rapidgator.Api;

public class ApiClient(
    IRapidgatorApi api,
    HttpClientProvider httpClientProvider,
    ILogger<ApiClient> logger
) : IRapidgatorApiClient
{
    private const int AuthTimeout = 400;

    private bool NeedsReauthentication =>
        string.IsNullOrWhiteSpace(authToken)
        || (DateTime.UtcNow - lastAuthTime).TotalSeconds > AuthTimeout;

    private string? authToken;

    private DateTime lastAuthTime = DateTime.MinValue;

    private readonly SemaphoreSlim authSemaphore = new(initialCount: 1, maxCount: 1);

    public async Task<UploadFileResponse> RequestUploadFileAsync(
        string name,
        long size,
        string hash,
        string? folderId,
        RapidgatorConfig config,
        CancellationToken cancellationToken
    )
    {
        var token = await GetAuthTokenAsync(config, cancellationToken);
        return await api.RequestUploadFileAsync(
            token: token,
            name: name,
            size: size,
            hash: hash,
            folderId: folderId,
            cancellationToken: cancellationToken
        );
    }

    public async Task<string> CreateFolderAsync(
        string folderName,
        RapidgatorConfig config,
        CancellationToken cancellationToken
    )
    {
        var token = await GetAuthTokenAsync(config, cancellationToken);
        var rootFolder = await api.GetFolderInfoAsync(
            token: token,
            folderId: null,
            cancellationToken: cancellationToken
        );

        EnsureFolderResponseSucceeded(rootFolder, "Rapidgator root folder lookup failed");

        var existingFolder = rootFolder
            .Response!.Folder!.Folders.Where(folder =>
                string.Equals(folder.Name, folderName, StringComparison.Ordinal)
            )
            .OrderBy(folder => folder.Created)
            .ThenBy(folder => folder.FolderId, StringComparer.Ordinal)
            .FirstOrDefault();

        if (existingFolder is not null)
        {
            return existingFolder.FolderId;
        }

        var createdFolder = await api.CreateFolderAsync(
            token: token,
            name: folderName,
            folderId: rootFolder.Response.Folder.FolderId,
            cancellationToken: cancellationToken
        );

        EnsureFolderResponseSucceeded(createdFolder, "Rapidgator folder creation failed");

        return createdFolder.Response!.Folder!.FolderId;
    }

    public async Task<UploadFileResponse> UploadFileAsync(
        string uploadUrl,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken
    )
    {
        using var httpClient = httpClientProvider.GetUploadClient();
        var httpResponse = await httpClient.PostAsync(
            uploadUrl,
            new MultipartFormDataContent { { new StreamContent(stream), "file", fileName } },
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
            options: new JsonSerializerOptions
            {
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                PropertyNameCaseInsensitive = true,
            }
        )!;

        if (!((HttpStatusCode)response.Status).IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Upload failed for file {fileName} with message: {response.Details}"
            );
        }

        return response;
    }

    public async Task<UploadFileResponse> GetUploadInfoAsync(
        RapidgatorConfig config,
        string uploadId,
        CancellationToken cancellationToken
    )
    {
        var token = await GetAuthTokenAsync(config, cancellationToken);
        var response = await api.GetFileStatusAsync(token, uploadId, cancellationToken);
        return response;
    }

    public async Task<IReadOnlyDictionary<string, bool>> CheckLinksAsync(
        RapidgatorConfig config,
        IReadOnlyList<string> links,
        CancellationToken cancellationToken
    )
    {
        var token = await GetAuthTokenAsync(config, cancellationToken);

        var responses = new List<CheckLinksResponse>();

        foreach (var linksBatch in links.Chunk(25))
        {
            var response = await api.CheckLinkAsync(
                token: token,
                links: string.Join(',', linksBatch),
                cancellationToken: cancellationToken
            );

            responses.Add(response.Content!);
        }

        return responses
            .Where(r => r.Responses is not null)
            .SelectMany(r => r.Responses!)
            .ToDictionary(r => r.Url, r => r.Status == "ACCESS");
    }

    private async Task<string> GetAuthTokenAsync(
        RapidgatorConfig config,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await authSemaphore.WaitAsync(cancellationToken);

            if (NeedsReauthentication)
            {
                logger.LogInformation(
                    "Authenticating to Rapidgator for user {Username}",
                    config.Username
                );
                var loginResponse = await LoginAsync(
                    login: config.Username,
                    password: config.Password,
                    cancellationToken: cancellationToken
                );

                authToken = loginResponse.Response.Token;
                lastAuthTime = DateTime.UtcNow;
            }

            return authToken!;
        }
        finally
        {
            authSemaphore.Release();
        }
    }

    private static void EnsureFolderResponseSucceeded(FolderResponse response, string message)
    {
        if (
            response.Status != (int)HttpStatusCode.OK
            || string.IsNullOrWhiteSpace(response.Response?.Folder?.FolderId)
        )
        {
            throw new HttpRequestException($"{message}: {response.Details}");
        }
    }

    private async Task<LoginResponse> LoginAsync(
        string login,
        string password,
        CancellationToken cancellationToken
    )
    {
        var response = await api.LoginAsync(login, password, cancellationToken);
        return response.Content!;
    }
}
