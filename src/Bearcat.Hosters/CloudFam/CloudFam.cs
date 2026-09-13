using System.Text.Json;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Hosters.CloudFam.Api;
using Bearcat.Hosters.Extensions;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.CloudFam;

public class CloudFam(ICloudFamApiClient apiClient, ILogger<CloudFam> logger) : IHosterWithFolders
{
    public string Name => "CloudFam.io";

    public bool SupportsPremiumOnlyDownloads => false;

    public IReadOnlyList<string> ConfigurationKeys => [nameof(CloudFamConfig.ApiKey)];

    public TimeSpan UploadRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    public bool HasFixedParallelUploadLimit => false;

    public int? DefaultMaximumParallelUploads => MaxParallelUploads;

    private const int MaxParallelUploads = 20;

    private const int UploadAttempts = 3;

    public async Task<UploadFileResult> UploadFileAsync(
        FileDto fileDto,
        IHosterConfig hosterConfig,
        IUploadProgress progress,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<CloudFamConfig>();
        var errors = new List<string>();
        var fileInfo = new FileInfo(fileDto.FullFileName);

        foreach (var attempt in Enumerable.Range(1, UploadAttempts))
        {
            try
            {
                logger.LogInformation(
                    "Uploading file {FilePath} to CloudFam (Attempt {Attempt})",
                    fileDto.FullFileName,
                    attempt
                );

                await using var stream = SequentialFileReader.OpenRead(fileDto.FullFileName);
                var uploadResult = await apiClient.UploadFileAsync(
                    config: config,
                    stream: new CountingStream(stream, progress),
                    fileName: fileInfo.Name,
                    fileSize: fileInfo.Length,
                    folderId: fileDto.FolderId,
                    cancellationToken: cancellationToken
                );

                return new UploadFileResult(
                    IsSuccess: true,
                    FileDto: fileDto,
                    ErrorMessages: [],
                    FileUrl: uploadResult.FileUrl,
                    ExternalId: uploadResult.FileId
                );
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var message = ex.InnerException?.Message ?? ex.Message;
                logger.LogError(
                    ex,
                    "Error while uploading file {FilePath} to CloudFam on attempt {Attempt}: {ErrorMessage}",
                    fileDto.FullFileName,
                    attempt,
                    message
                );

                errors.Add(message);
            }

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
        var config = hosterConfig.As<CloudFamConfig>();

        try
        {
            var checkResults = await apiClient.CheckLinksAsync(config, files, cancellationToken);

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
                ErrorMessages: [ex.InnerException?.Message ?? ex.Message],
                StatusPerFileUrl: new Dictionary<string, bool>()
            );
        }
    }

    public IHosterConfig DeserializeHosterConfig(string serializedConfig)
    {
        var config = JsonSerializer.Deserialize<CloudFamConfig>(serializedConfig);

        return config
            ?? throw new InvalidOperationException("Failed to deserialize CloudFam config");
    }

    public string SerializeHosterConfig(Dictionary<string, string> hosterConfig)
    {
        var config = new CloudFamConfig
        {
            ApiKey = hosterConfig.GetValueOrDefault(nameof(CloudFamConfig.ApiKey)) ?? string.Empty,
        };

        return JsonSerializer.Serialize(config);
    }

    public Task<int?> GetMaximumParallelUploadsAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(DefaultMaximumParallelUploads);
    }

    public async Task<string> CreateFolderAsync(
        string folderName,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<CloudFamConfig>();

        return await apiClient.CreateFolderAsync(config, folderName, cancellationToken);
    }

    public async Task MoveFileToFolderAsync(
        string fileUrl,
        string? externalId,
        string folderId,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<CloudFamConfig>();

        await apiClient.MoveFileToFolderAsync(
            config,
            fileUrl,
            externalId,
            folderId,
            cancellationToken
        );
    }

    public async Task<TryLoginResult> TryLoginAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<CloudFamConfig>();

        try
        {
            var success = await apiClient.IsApiKeyValidAsync(config, cancellationToken);

            return new TryLoginResult(
                IsSuccess: success,
                ErrorMessage: success ? null : "Invalid credentials"
            );
        }
        catch (Exception ex)
        {
            return new TryLoginResult(
                IsSuccess: false,
                ErrorMessage: ex.InnerException?.Message ?? ex.Message
            );
        }
    }
}
