using System.Net;
using System.Text.Json;
using Bearcat.Abstractions;
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

    public TimeSpan UploadStatusPollDelay { get; set; } = TimeSpan.FromSeconds(1);

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

        try
        {
            var checkResults = await apiClient.CheckLinksAsync(
                config: config,
                files: files,
                cancellationToken: cancellationToken
            );

            var statusPerFileUrl = checkResults.ToDictionary(
                result => result.Key,
                result => result.Value.IsOnline
            );

            var downloadCountPerFileUrl = checkResults
                .Where(result => result.Value.DownloadCount is not null)
                .ToDictionary(result => result.Key, result => result.Value.DownloadCount!.Value);

            return new FileExistResult(
                IsSuccess: true,
                ErrorMessages: [],
                StatusPerFileUrl: statusPerFileUrl,
                DownloadCountPerFileUrl: downloadCountPerFileUrl
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

        return response.Content!.Response.User.Upload.NbPipes;
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

    public async Task MoveFileToFolderAsync(
        string fileUrl,
        string? externalId,
        string folderId,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<RapidgatorConfig>();

        await apiClient.MoveFileToFolderAsync(config, fileUrl, folderId, cancellationToken);
    }

    private async Task<UploadFileResult> UploadFileInternalAsync(
        FileDto fileDto,
        RapidgatorConfig config,
        IUploadProgress progress,
        CancellationToken cancellationToken
    )
    {
        await using var stream = SequentialFileReader.OpenRead(fileDto.FullFileName);

        var uploadRequest = await apiClient.RequestUploadFileAsync(
            name: Path.GetFileName(fileDto.FullFileName),
            size: stream.Length,
            hash: await Md5FileHash.GetOrComputeAsync(fileDto.Md5Hash, stream, cancellationToken),
            folderId: fileDto.FolderId,
            config: config,
            cancellationToken: cancellationToken
        );

        var requestedUpload = uploadRequest.Response?.Upload;

        if (HasUploadedFile(requestedUpload?.File))
        {
            return new UploadFileResult(
                IsSuccess: true,
                FileDto: fileDto,
                ErrorMessages: [],
                FileUrl: ShortenFileUrl(
                    fileUrl: requestedUpload!.File?.Url,
                    fileName: Path.GetFileName(fileDto.FullFileName)
                )
            );
        }

        if (requestedUpload?.Url is null)
        {
            throw new RetryException(
                uploadRequest.Details ?? requestedUpload?.StateLabel ?? string.Empty
            );
        }

        logger.LogInformation(
            "Uploading file {FileName} to Rapidgator with URL {Url}",
            fileDto.FullFileName,
            requestedUpload.Url
        );

        var uploadResult = await apiClient.UploadFileAsync(
            uploadUrl: requestedUpload.Url,
            stream: new CountingStream(stream, progress),
            fileName: Path.GetFileName(fileDto.FullFileName),
            cancellationToken: cancellationToken
        );

        logger.LogInformation(
            "Finished uploading file {FileName} with Result JSON: {Json}, wait for processing to finish",
            fileDto.FullFileName,
            JsonSerializer.Serialize(uploadResult)
        );

        var uploadStatus = uploadResult;

        while (GetUploadState(uploadStatus) is UploadStates.Uploading or UploadStates.Processing)
        {
            logger.LogInformation(
                "File upload for file {File} still in progress, state label: {Label}. state: {State}, waiting for it to finish. JSON: {Json}",
                fileDto.FullFileName,
                uploadStatus.Response?.Upload?.StateLabel,
                GetUploadState(uploadStatus),
                JsonSerializer.Serialize(uploadStatus.Response)
            );

            await Task.Delay(UploadStatusPollDelay, cancellationToken);

            try
            {
                uploadStatus = await apiClient.GetUploadInfoAsync(
                    uploadId: requestedUpload.UploadId,
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
        }

        logger.LogInformation(
            "Finished upload of file {FileName} to Rapidgator with status {Status}. JSON: {Json}",
            fileDto.FullFileName,
            GetUploadState(uploadStatus),
            JsonSerializer.Serialize(uploadStatus.Response)
        );

        var completedFile = GetUploadedFile(uploadStatus) ?? GetUploadedFile(uploadResult);

        var uploadError = uploadStatus.Response?.Upload?.Error;

        if (completedFile is null && uploadError is not null)
        {
            throw new RetryException(FormatUploadError(uploadError));
        }

        if (completedFile is null)
        {
            throw new RetryException(
                uploadStatus.Details
                    ?? uploadStatus.Response?.Upload?.StateLabel
                    ?? uploadResult.Details
                    ?? $"Rapidgator upload ended with state {GetUploadState(uploadStatus)}"
            );
        }

        var errors = new List<string>();

        var changeModeSucceeded = true;

        if (fileDto.PremiumOnlyDownload)
        {
            var changeFileModeErrors = await ChangeFileModeAsync(
                fileId: completedFile.FileId,
                config: config,
                cancellationToken: cancellationToken
            );

            changeModeSucceeded = changeFileModeErrors.Count == 0;
            errors.AddRange(changeFileModeErrors);
        }

        return new UploadFileResult(
            IsSuccess: changeModeSucceeded,
            FileDto: fileDto,
            ErrorMessages: errors,
            FileUrl: ShortenFileUrl(
                fileUrl: completedFile.Url,
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

    private static string? ShortenFileUrl(string? fileUrl, string fileName)
    {
        if (fileUrl is null)
        {
            return null;
        }

        var fileNameWithHtml = $"/{Path.GetFileName(fileName)}.html";
        return fileUrl.Replace(fileNameWithHtml, string.Empty);
    }

    private static string FormatUploadError(UploadFileResponse.UploadError error)
    {
        var message = string.IsNullOrWhiteSpace(error.Message)
            ? "Rapidgator did not provide an error message"
            : error.Message;

        return error.Code > 0
            ? $"Rapidgator upload failed: {message} (code {error.Code})"
            : $"Rapidgator upload failed: {message}";
    }

    private static int? GetUploadState(UploadFileResponse response)
    {
        return response.Response?.Upload?.State ?? response.Response?.State;
    }

    private static UploadFileResponse.File? GetUploadedFile(UploadFileResponse response)
    {
        var nestedFile = response.Response?.Upload?.File;

        if (HasUploadedFile(nestedFile))
        {
            return nestedFile;
        }

        var directFile = response.Response?.File;
        return HasUploadedFile(directFile) ? directFile : null;
    }

    private static bool HasUploadedFile(UploadFileResponse.File? file)
    {
        return !string.IsNullOrWhiteSpace(file?.FileId) && !string.IsNullOrWhiteSpace(file.Url);
    }
}
