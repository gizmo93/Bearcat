using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Hosters.Extensions;
using Bearcat.Hosters.Rapidgator.Api;
using Bearcat.Hosters.Rapidgator.Api.File;
using Bearcat.Hosters.Rapidgator.Exceptions;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Refit;

namespace Bearcat.Hosters.Rapidgator;

public class Rapidgator(
    IRapidgatorApiClient apiClient,
    IRapidgatorApi rapidgatorApi,
    ILogger<Rapidgator> logger
) : IHosterWithFolders
{
    public string Name => "Rapidgator";

    public bool SupportsPremiumOnlyDownloads => true;

    public IReadOnlyList<string> ConfigurationKeys =>
        [nameof(RapidgatorConfig.Username), nameof(RapidgatorConfig.Password)];

    public TimeSpan UploadRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan UploadStatusPollDelay { get; set; } = TimeSpan.FromSeconds(5);

    public async Task<UploadFileResult> UploadFileAsync(
        FileDto fileDto,
        IHosterConfig hosterConfig,
        IUploadProgress progress,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<RapidgatorConfig>();

        var errors = new List<string>();

        foreach (var attempt in Enumerable.Range(1, 3))
        {
            try
            {
                return await UploadFileInternalAsync(
                    fileDto: fileDto,
                    config: config,
                    progress: progress,
                    cancellationToken: cancellationToken
                );
            }
            catch (RetryException ex)
            {
                logger.LogWarning(
                    ex,
                    "Retryable error occurred on attempt {Attempt} for file {FileName}: {Error}",
                    attempt,
                    fileDto.FullFileName,
                    ex.InnerException?.Message ?? ex.Message
                );

                errors.Add(ex.InnerException?.Message ?? ex.Message);
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(
                    ex,
                    "HTTP request failed on attempt {Attempt} for file {FileName}: {Message}",
                    attempt,
                    fileDto.FullFileName,
                    ex.InnerException?.Message ?? ex.Message
                );

                errors.Add(ex.InnerException?.Message ?? ex.Message);
            }
            catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                logger.LogError(
                    ex,
                    "Service unavailable on attempt {Attempt} for file {FileName}: {Message}",
                    attempt,
                    fileDto.FullFileName,
                    ex.Message
                );
            }

            logger.LogInformation(
                "Retrying upload for file {FileName}, current attempt {Attempt}",
                fileDto.FullFileName,
                attempt
            );

            await Task.Delay(UploadRetryDelay, cancellationToken);
        }

        return new UploadFileResult(
            IsSuccess: false,
            FileDto: fileDto,
            ErrorMessages: errors,
            FileUrl: null
        );
    }

    public async Task<FileExistResult> CheckFilesExistAsync(
        IHosterConfig hosterConfig,
        IReadOnlyList<FileUrlToCheckDto> files,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<RapidgatorConfig>();
        var fileUrls = files.Select(file => file.Url).ToList();

        try
        {
            var statusPerLink = await apiClient.CheckLinksAsync(
                config: config,
                links: fileUrls,
                cancellationToken: cancellationToken
            );

            return new FileExistResult(
                IsSuccess: true,
                ErrorMessages: [],
                StatusPerFileUrl: statusPerLink
            );
        }
        catch (Exception ex)
        {
            return new FileExistResult(
                IsSuccess: false,
                ErrorMessages: [$"Login failed: {ex.Message}"],
                StatusPerFileUrl: new Dictionary<string, bool>()
            );
        }
    }

    public IHosterConfig DeserializeHosterConfig(string serializedConfig)
    {
        var config = JsonSerializer.Deserialize<RapidgatorConfig>(serializedConfig);

        return config
            ?? throw new InvalidOperationException("Failed to deserialize Rapidgator config");
    }

    public string SerializeHosterConfig(Dictionary<string, string> hosterConfig)
    {
        var config = new RapidgatorConfig
        {
            Username = hosterConfig.GetValueOrDefault("Username") ?? string.Empty,
            Password = hosterConfig.GetValueOrDefault("Password") ?? string.Empty,
        };

        return JsonSerializer.Serialize(config);
    }

    public bool HasFixedParallelUploadLimit => true;

    public int? DefaultMaximumParallelUploads => null;

    public async Task<int?> GetMaximumParallelUploadsAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<RapidgatorConfig>();

        var response = await rapidgatorApi.LoginAsync(
            login: config.Username,
            password: config.Password,
            cancellationToken: cancellationToken
        );

        return response.Content!.Response.User.RemoteUpload.MaxNbJobs;
    }

    public async Task<TryLoginResult> TryLoginAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
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
                cancellationToken: cancellationToken
            );

            return new TryLoginResult(
                IsSuccess: response.Content!.Status == (int)HttpStatusCode.OK,
                ErrorMessage: response.Content.Details
            );
        }
        catch (Exception e)
        {
            return new TryLoginResult(IsSuccess: false, ErrorMessage: e.Message);
        }
    }

    public async Task<string> CreateFolderAsync(
        string folderName,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<RapidgatorConfig>();

        return await apiClient.CreateFolderAsync(
            folderName: folderName,
            config: config,
            cancellationToken: cancellationToken
        );
    }

    private async Task<UploadFileResult> UploadFileInternalAsync(
        FileDto fileDto,
        RapidgatorConfig config,
        IUploadProgress progress,
        CancellationToken cancellationToken
    )
    {
        await using var stream = File.OpenRead(fileDto.FullFileName);

        var uploadRequest = await apiClient.RequestUploadFileAsync(
            name: Path.GetFileName(fileDto.FullFileName),
            size: stream.Length,
            hash: await CreateMd5HashAsync(stream, cancellationToken),
            folderId: fileDto.FolderId,
            config: config,
            cancellationToken: cancellationToken
        );

        if (uploadRequest.Response?.Upload?.File?.FileId is not null)
        {
            return new UploadFileResult(
                IsSuccess: false,
                FileDto: fileDto,
                ErrorMessages: ["File already exists"],
                FileUrl: null
            );
        }

        if (uploadRequest.Response?.Upload?.Url is null)
        {
            throw new RetryException(
                uploadRequest.Details ?? uploadRequest.Response?.Upload?.StateLabel ?? string.Empty
            );
        }

        logger.LogInformation(
            "Uploading file {FileName} to Rapidgator with URL {Url}",
            fileDto.FullFileName,
            uploadRequest.Response.Upload.Url
        );

        var uploadResult = await apiClient.UploadFileAsync(
            uploadUrl: uploadRequest.Response.Upload.Url,
            stream: new CountingStream(stream, progress),
            fileName: Path.GetFileName(fileDto.FullFileName),
            cancellationToken: cancellationToken
        );

        logger.LogInformation(
            "Finished uploading file {FileName} with Result JSON: {Json}, wait for processing to finish",
            fileDto.FullFileName,
            JsonSerializer.Serialize(uploadResult)
        );

        UploadFileResponse uploadStatus;

        while (true)
        {
            try
            {
                uploadStatus = await apiClient.GetUploadInfoAsync(
                    uploadId: uploadResult.Response?.Upload?.UploadId
                        ?? uploadRequest.Response.Upload.UploadId,
                    config: config,
                    cancellationToken: cancellationToken
                );
            }
            catch (ApiException ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to get upload status for file {FileName}: {Message}",
                    fileDto.FullFileName,
                    ex.InnerException?.Message ?? ex.Message
                );
                continue;
            }

            if (uploadStatus.Response?.Upload?.State != UploadStates.Processing)
            {
                break;
            }

            logger.LogInformation(
                "File upload for file {File} still in progress, state label: {Label}. state: {State}, waiting for it to finish. JSON: {Json}",
                fileDto.FullFileName,
                uploadStatus.Response.Upload.StateLabel,
                uploadStatus.Response.Upload.State,
                JsonSerializer.Serialize(uploadStatus.Response)
            );

            await Task.Delay(UploadStatusPollDelay, cancellationToken);
        }

        logger.LogInformation(
            "Finished upload of file {FileName} to Rapidgator with status {Status}",
            fileDto.FullFileName,
            uploadStatus.Response?.Upload?.State
        );

        var errors = new List<string?> { uploadResult.Details, uploadStatus.Details }
            .OfType<string>()
            .ToList();

        var changeModeSucceeded = true;

        if (
            uploadStatus.Response?.Upload?.State == UploadStates.Done
            && fileDto.PremiumOnlyDownload
        )
        {
            var fileId = uploadStatus.Response.Upload.File?.FileId;
            var changeFileModeErrors = await ChangeFileModeAsync(
                fileId: fileId,
                config: config,
                cancellationToken: cancellationToken
            );

            changeModeSucceeded = changeFileModeErrors.Count == 0;
            errors.AddRange(changeFileModeErrors);
        }

        return new UploadFileResult(
            IsSuccess: uploadStatus.Response?.Upload?.State == UploadStates.Done
                && changeModeSucceeded,
            FileDto: fileDto,
            ErrorMessages: errors,
            FileUrl: ShortenFileUrl(
                fileUrl: uploadStatus.Response?.Upload?.File?.Url,
                fileName: Path.GetFileName(fileDto.FullFileName)
            )
        );
    }

    private async Task<IReadOnlyList<string>> ChangeFileModeAsync(
        string? fileId,
        RapidgatorConfig config,
        CancellationToken cancellationToken
    )
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(fileId))
        {
            errors.Add("Failed to change Rapidgator file mode: missing file id");
            return errors;
        }

        var changeModeResponse = await apiClient.ChangeFileModeAsync(
            config: config,
            fileId: fileId,
            mode: UploadMode.PremiumOnly,
            cancellationToken: cancellationToken
        );

        if (!((HttpStatusCode)changeModeResponse.Status).IsSuccessStatusCode)
        {
            errors.Add(changeModeResponse.Details ?? "Failed to change Rapidgator file mode");
        }

        return errors;
    }

    private static async Task<string> CreateMd5HashAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        using var md5 = MD5.Create();
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
