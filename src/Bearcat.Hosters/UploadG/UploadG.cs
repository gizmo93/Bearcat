using System.Text.Json;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Hosters.Extensions;
using Bearcat.Hosters.Shared;
using Bearcat.Hosters.UploadG.Api;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.UploadG;

public class UploadG(IUploadGApiClient apiClient, ILogger<UploadG> logger) : IHosterWithFolders
{
    private const int MaxParallelUploads = 10;

    public string Name => "UploadG.com";

    public bool SupportsPremiumOnlyDownloads => false;

    public IReadOnlyList<string> ConfigurationKeys => [nameof(UploadGConfig.ApiKey)];

    public TimeSpan UploadRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    public async Task<UploadFileResult> UploadFileAsync(
        FileDto fileDto,
        IHosterConfig hosterConfig,
        IUploadProgress progress,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<UploadGConfig>();
        var errors = new List<string>();
        var fileInfo = new FileInfo(fileDto.FullFileName);

        foreach (var attempt in Enumerable.Range(1, 3))
        {
            try
            {
                logger.LogInformation(
                    "Uploading file {FilePath} to UploadG (Attempt {Attempt})",
                    fileDto.FullFileName,
                    attempt
                );

                await using var stream = SequentialFileReader.OpenRead(fileDto.FullFileName);
                var uploadResponse = await apiClient.UploadFileAsync(
                    config: config,
                    stream: new CountingStream(stream, progress),
                    fileName: fileInfo.Name,
                    folderId: fileDto.FolderId,
                    fileSize: fileInfo.Length,
                    cancellationToken: cancellationToken
                );

                if (uploadResponse.FileEntry is null)
                {
                    throw new HttpRequestException("UploadG upload returned no file entry");
                }

                var fileUrl = await apiClient.GetOrCreateShareableLinkAsync(
                    config: config,
                    entryId: uploadResponse.FileEntry.Id,
                    cancellationToken: cancellationToken
                );

                return new UploadFileResult(
                    IsSuccess: true,
                    FileDto: fileDto,
                    ErrorMessages: [],
                    FileUrl: fileUrl,
                    ExternalId: uploadResponse.FileEntry.Id.ToString()
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
                    "Error while uploading file {FilePath} to UploadG on attempt {Attempt}: {ErrorMessage}",
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
        var config = hosterConfig.As<UploadGConfig>();

        try
        {
            var statusPerFileUrl = await apiClient.CheckLinksAsync(
                config: config,
                files: files,
                cancellationToken: cancellationToken
            );

            return new FileExistResult(
                IsSuccess: true,
                ErrorMessages: [],
                StatusPerFileUrl: statusPerFileUrl
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
        var config = JsonSerializer.Deserialize<UploadGConfig>(serializedConfig);

        return config
            ?? throw new InvalidOperationException("Failed to deserialize UploadG config");
    }

    public string SerializeHosterConfig(Dictionary<string, string> hosterConfig)
    {
        var config = new UploadGConfig
        {
            ApiKey = hosterConfig.GetValueOrDefault(nameof(UploadGConfig.ApiKey)) ?? string.Empty,
        };

        return JsonSerializer.Serialize(config);
    }

    public bool HasFixedParallelUploadLimit => false;

    public int? DefaultMaximumParallelUploads => MaxParallelUploads;

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
        var config = hosterConfig.As<UploadGConfig>();

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
        if (string.IsNullOrWhiteSpace(externalId))
        {
            throw new InvalidOperationException(
                $"Cannot move UploadG file {fileUrl} without a known entry id"
            );
        }

        var config = hosterConfig.As<UploadGConfig>();

        await apiClient.MoveFileToFolderAsync(config, externalId, folderId, cancellationToken);
    }

    public async Task<TryLoginResult> TryLoginAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<UploadGConfig>();

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
