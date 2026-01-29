using System.Net;
using System.Text.Json;
using Bearcat.Domain.Abstractions.Hoster;
using Bearcat.Domain.Abstractions.Hoster.Results;
using Bearcat.Domain.Entities;
using Bearcat.Hosters.Rapidgator.ApiClient;
using Bearcat.Hosters.Rapidgator.ApiClient.File;
using Bearcat.Hosters.Rapidgator.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Rapidgator;

public class Rapidgator(
    RapidgatorApiClient rapidgatorApiClient,
    IRapidgatorApi rapidgatorApi,
    ILogger<Rapidgator> logger) : IHoster
{
    public string Name => "Rapidgator";

    public IReadOnlyList<string> ConfigurationKeys => [
        nameof(RapidgatorConfig.Username),
        nameof(RapidgatorConfig.Password)
    ];

    public async Task<UploadFileResult> UploadFileAsync(
        ArchiveFile archiveFile,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken)
    {
        if (hosterConfig is not RapidgatorConfig config)
        {
            throw new ArgumentException("Invalid hoster config for Rapidgator", nameof(hosterConfig));
        }

        var errors = new List<string>();

        foreach (var attempt in Enumerable.Range(1, 3))
        {
            try
            {
                return await UploadFileInternalAsync(
                    archiveFile: archiveFile,
                    config: config,
                    cancellationToken: cancellationToken);
            }
            catch (RetryException ex)
            {
                logger.LogWarning("Retryable error occurred on attempt {Attempt} for file {FileName}: {Error}",
                    attempt,
                    archiveFile.FullFileName,
                    ex.InnerException?.Message ?? ex.Message);

                errors.Add(ex.InnerException?.Message ?? ex.Message);
            }
            catch (HttpRequestException ex)
            {
                logger.LogError("HTTP request failed on attempt {Attempt} for file {FileName}: {Message}",
                    attempt,
                    archiveFile.FullFileName,
                    ex.InnerException?.Message ?? ex.Message);

                errors.Add(ex.InnerException?.Message ?? ex.Message);
            }
            catch (Refit.ApiException ex)
                when (ex.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                logger.LogError("Service unavailable on attempt {Attempt} for file {FileName}: {Message}",
                    attempt,
                    archiveFile.FullFileName,
                    ex.Message);
            }

            logger.LogInformation("Retrying upload for file {FileName}, current attempt {Attempt}",
                archiveFile.FullFileName,
                attempt);

            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        }

        return new UploadFileResult(
            IsSuccess: false,
            ArchiveFile: archiveFile,
            ErrorMessages: errors,
            FileUrl: null);
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

        try
        {
            var statusPerLink = await rapidgatorApiClient.CheckLinksAsync(
                config: rapidgatorConfig,
                links: fileUrls,
                cancellationToken: cancellationToken);

            return new FileExistResult(
                IsSuccess: true,
                ErrorMessages: [],
                StatusPerFileUrl: statusPerLink);
        }
        catch (Exception ex)
        {
            return new FileExistResult(
                IsSuccess: false,
                ErrorMessages: [$"Login failed: {ex.Message}"],
                StatusPerFileUrl: new Dictionary<string, bool>());
        }
    }

    public IHosterConfig DeserializeHosterConfig(string serializedConfig)
    {
        var config = JsonSerializer.Deserialize<RapidgatorConfig>(serializedConfig);

        return config ?? throw new InvalidOperationException("Failed to deserialize Rapidgator config");
    }

    public string SerializeHosterConfig(Dictionary<string, string> hosterConfig)
    {
        var config = new RapidgatorConfig
        {
            Username = hosterConfig.GetValueOrDefault("Username") ?? string.Empty,
            Password = hosterConfig.GetValueOrDefault("Password") ?? string.Empty
        };

        return JsonSerializer.Serialize(config);
    }

    public async Task<int?> GetMaximumParallelUploadsAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken)
    {
        if (hosterConfig is not RapidgatorConfig rapidgatorConfig)
        {
            throw new InvalidOperationException("Invalid hoster config for Rapidgator");
        }

        var response = await rapidgatorApi.LoginAsync(
            login: rapidgatorConfig.Username,
            password: rapidgatorConfig.Password,
            cancellationToken: cancellationToken);

        return response.Content!.Response.User.RemoteUpload.MaxNbJobs;
    }

    public async Task<TryLoginResult> TryLoginAsync(IHosterConfig hosterConfig, CancellationToken cancellationToken)
    {
        if (hosterConfig is not RapidgatorConfig rapidgatorConfig)
        {
            throw new InvalidOperationException("Invalid hoster config for Rapidgator");
        }

        try
        {
            var response = await rapidgatorApi.LoginAsync(
                rapidgatorConfig.Username,
                rapidgatorConfig.Password,
                cancellationToken: cancellationToken);

            return new TryLoginResult(
                IsSuccess: response.Content!.Status == (int)HttpStatusCode.OK,
                ErrorMessage: response.Content.Details);
        }
        catch (Exception e)
        {
            return new TryLoginResult(IsSuccess: false, ErrorMessage: e.Message);
        }
    }

    private async Task<UploadFileResult> UploadFileInternalAsync(
        ArchiveFile archiveFile,
        RapidgatorConfig config,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(archiveFile.FullFileName);

        var uploadRequest = await rapidgatorApiClient.RequestUploadFileAsync(
            name: Path.GetFileName(archiveFile.FullFileName),
            size: stream.Length,
            hash: await CreateMd5HashAsync(stream, cancellationToken),
            config: config,
            cancellationToken: cancellationToken);

        if (uploadRequest.Response?.Upload?.File?.FileId is not null)
        {
            return new UploadFileResult(
                IsSuccess: false,
                ArchiveFile: archiveFile,
                ErrorMessages: ["File already exists"],
                FileUrl: null);
        }

        if (uploadRequest.Response?.Upload?.Url is null)
        {
            throw new RetryException(uploadRequest.Details ??
                                     uploadRequest?.Response?.Upload?.StateLabel ?? string.Empty);
        }

        logger.LogInformation("Uploading file {FileName} to Rapidgator with URL {Url}",
            archiveFile.FullFileName,
            uploadRequest.Response.Upload.Url);

        var uploadResult = await rapidgatorApiClient.UploadFileAsync(
            uploadUrl: uploadRequest.Response.Upload.Url,
            stream: stream,
            fileName: Path.GetFileName(archiveFile.FullFileName),
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Finished uploading file {FileName} with Result JSON: {Json}, wait for processing to finish",
            archiveFile.FullFileName,
            JsonSerializer.Serialize(uploadResult));

        UploadFileResponse uploadStatus;

        while (true)
        {
            try
            {
                uploadStatus = await rapidgatorApiClient.GetUploadInfoAsync(
                    uploadId: uploadResult.Response?.Upload?.UploadId ?? uploadRequest.Response.Upload.UploadId,
                    config: config,
                    cancellationToken: cancellationToken);
            }
            catch (Refit.ApiException ex)
            {
                logger.LogWarning("Failed to get upload status for file {FileName}: {Message}",
                    archiveFile.FullFileName,
                    ex.InnerException?.Message ?? ex.Message);
                continue;
            }

            if (uploadStatus.Response?.Upload?.State != UploadStates.Processing)
            {
                break;
            }

            logger.LogInformation(
                "File upload for file {File} still in progress, state label: {Label}. state: {State}, waiting for it to finish. JSON: {Json}",
                archiveFile.FullFileName,
                uploadStatus.Response.Upload.StateLabel,
                uploadStatus.Response.Upload.State,
                JsonSerializer.Serialize(uploadStatus.Response));

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }

        logger.LogInformation("Finished upload of file {FileName} to Rapidgator with status {Status}",
            archiveFile.FullFileName,
            uploadStatus?.Response?.Upload?.State);

        var errors = new List<string?> { uploadResult.Details, uploadStatus?.Details }
            .OfType<string>()
            .ToList();

        return new UploadFileResult(
            IsSuccess: uploadStatus?.Response?.Upload?.State == UploadStates.Done,
            ArchiveFile: archiveFile,
            ErrorMessages: errors,
            FileUrl: ShortenFileUrl(
                fileUrl: uploadStatus?.Response?.Upload?.File?.Url,
                fileName: Path.GetFileName(archiveFile.FullFileName)));
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

    private static string? ShortenFileUrl(string? fileUrl, string fileName)
    {
        if (fileUrl is null)
        {
            return null;
        }
        
        var fileNameWithHtml = $"/{Path.GetFileName(fileName)}.html";
        return fileUrl.Replace(fileNameWithHtml, string.Empty);
    }
}
