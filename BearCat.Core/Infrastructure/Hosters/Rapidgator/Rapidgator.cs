using System.Net;
using System.Text.Json;
using BearCat.Core.Domain.Abstractions.Hoster;
using BearCat.Core.Domain.Abstractions.Hoster.Results;
using BearCat.Core.Infrastructure.Hosters.Rapidgator.ApiClient;
using BearCat.Core.Infrastructure.Hosters.Rapidgator.ApiClient.File;
using BearCat.Core.Infrastructure.Hosters.Rapidgator.ApiClient.User;
using Microsoft.Extensions.Logging;

namespace BearCat.Core.Infrastructure.Hosters.Rapidgator;

public class Rapidgator(
    RapidgatorApiClient rapidgatorApiClient,
    IRapidgatorApi rapidgatorApi,
    ILogger<Rapidgator> logger) : IHoster
{
    private static readonly HashSet<int> FinishedStatuses = [UploadStates.Done, UploadStates.Failed];

    public string Name => "Rapidgator";

    public async Task<UploadFileResult> UploadFileAsync(
        IHosterConfig hosterConfig,
        string fullFilePath,
        CancellationToken cancellationToken)
    {
        if (hosterConfig is not RapidgatorConfig rapidgatorConfig)
        {
            return new UploadFileResult(
                IsSuccess: false,
                ErrorMessages: ["Invalid hoster config for Rapidgator"],
                FileUrl: null);
        }

        var login = await LoginAsync(rapidgatorConfig, cancellationToken);

        await using var stream = File.OpenRead(fullFilePath);

        var uploadRequest = await rapidgatorApiClient.RequestUploadFileAsync(
            token: login.Response.Token,
            name: Path.GetFileName(fullFilePath),
            size: stream.Length,
            hash: await CreateMd5HashAsync(stream, cancellationToken),
            cancellationToken: cancellationToken);

        if (uploadRequest.Response?.Upload is null)
        {
            return new UploadFileResult(
                IsSuccess: false,
                ErrorMessages: [uploadRequest.Details ?? string.Empty],
                FileUrl: null);
        }

        await rapidgatorApiClient.UploadFileAsync(
            uploadUrl: uploadRequest.Response.Upload.Url,
            stream: stream,
            fileName: Path.GetFileName(fullFilePath),
            cancellationToken: cancellationToken);

        var uploadStatus = await rapidgatorApiClient.GetUploadInfoAsync(
            token: login.Response.Token,
            uploadId: uploadRequest.Response.Upload.UploadId,
            cancellationToken: cancellationToken);

        while (uploadStatus.Response?.Upload is not null &&
               !FinishedStatuses.Contains(uploadStatus.Response.Upload.State))
        {
            logger.LogDebug("File upload still in progress, waiting for it to finish");

            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);

            uploadStatus = await rapidgatorApiClient.GetUploadInfoAsync(
                token: login.Response.Token,
                uploadId: uploadRequest.Response.Upload.UploadId,
                cancellationToken: cancellationToken);
        }

        if (uploadStatus.Response is null)
        {
            return new UploadFileResult(
                IsSuccess: false,
                ErrorMessages: [uploadStatus.Details ?? string.Empty],
                FileUrl: null);
        }

        return new UploadFileResult(
            IsSuccess: true,
            ErrorMessages: [],
            FileUrl: uploadStatus.Response!.Upload!.File!.Url);
    }

    public async Task<FileExistResult> CheckFilesExistAsync(
        IHosterConfig hosterConfig,
        IReadOnlyList<string> fileUrls,
        CancellationToken cancellationToken)
    {
        if (hosterConfig is not RapidgatorConfig rapidgatorConfig)
        {
            return new FileExistResult(
                IsSuccess: false,
                ErrorMessages: ["Invalid hoster config for Rapidgator"],
                StatusPerFileUrl: new Dictionary<string, bool>());
        }

        var login = await LoginAsync(rapidgatorConfig, cancellationToken);

        var responses = new List<CheckLinksResponse>();

        foreach (var urlsBatch in fileUrls.Chunk(25))
        {
            var response = await rapidgatorApi.CheckLinkAsync(
                token: login.Response.Token,
                links: urlsBatch,
                cancellationToken: cancellationToken);

            responses.Add(response.Content!);
        }

        var statusPerFileUrl = responses
            .Where(r => r.Responses is not null)
            .SelectMany(r => r.Responses!)
            .ToDictionary(r => r.Filename, r => r.Status == "ACCESS");

        return new FileExistResult(
            IsSuccess: true,
            ErrorMessages: [],
            StatusPerFileUrl: statusPerFileUrl);
    }

    public IHosterConfig DeserializeHosterConfig(string serializedConfig)
    {
        var config = JsonSerializer.Deserialize<RapidgatorConfig>(serializedConfig);

        return config ?? throw new InvalidOperationException("Failed to deserialize Rapidgator config");
    }

    public string SerializeHosterConfig(IHosterConfig hosterConfig)
    {
        return JsonSerializer.Serialize(hosterConfig);
    }

    public async Task<int?> GetMaximumParallelUploadsAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken)
    {
        if (hosterConfig is not RapidgatorConfig rapidgatorConfig)
        {
            throw new InvalidOperationException("Invalid hoster config for Rapidgator");
        }

        var login = await LoginAsync(rapidgatorConfig, cancellationToken);

        return login.Response.User.RemoteUpload.MaxNbJobs;
    }

    public async Task<TryLoginResult> TryLoginAsync(IHosterConfig hosterConfig, CancellationToken cancellationToken)
    {
        if (hosterConfig is not RapidgatorConfig rapidgatorConfig)
        {
            throw new InvalidOperationException("Invalid hoster config for Rapidgator");
        }

        try
        {
            var loginResponse = await LoginAsync(rapidgatorConfig, cancellationToken);

            return new TryLoginResult(
                IsSuccess: loginResponse.Status == (int)HttpStatusCode.OK,
                ErrorMessage: loginResponse.Details);
        }
        catch (Exception e)
        {
            return new TryLoginResult(IsSuccess: false, ErrorMessage: e.Message);
        }
    }

    private async Task<LoginResponse> LoginAsync(
        RapidgatorConfig config,
        CancellationToken cancellationToken)
    {
        return await rapidgatorApiClient.LoginAsync(
            login: config.Username,
            password: config.Password,
            cancellationToken: cancellationToken);
    }

    private static async Task<string> CreateMd5HashAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hashBytes = await md5.ComputeHashAsync(stream, cancellationToken);
        stream.Seek(0, SeekOrigin.Begin);
        return Convert.ToHexStringLower(hashBytes);
    }
}
