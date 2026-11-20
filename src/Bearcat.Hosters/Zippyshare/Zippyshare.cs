using System.Text.Json;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Abstractions.Transfers;
using Bearcat.Hosters.Extensions;
using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Zippyshare.Api;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Zippyshare;

public class Zippyshare(IZippyshareApiClient apiClient, ILogger<Zippyshare> logger)
    : IHosterWithFileInfo,
        IHosterWithStorageQuota,
        IHosterWithFileSizeLimit
{
    private const int MaxParallelUploads = 3;

    private const int UploadAttempts = 3;

    public string Name => "Zippyshare.day";

    public IReadOnlyList<string> FileLinkHostNames => ["zippyshare.day"];

    public bool SupportsPremiumOnlyDownloads => false;

    public IReadOnlyList<string> ConfigurationKeys => [nameof(ZippyshareConfig.ApiKey)];

    public TimeSpan UploadRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    public bool HasFixedParallelUploadLimit => false;

    public int? DefaultMaximumParallelUploads => MaxParallelUploads;

    public int MaxFileSizeMb => 104;

    public async Task<UploadFileResult> UploadFileAsync(
        FileDto fileDto,
        IHosterConfig hosterConfig,
        ITransferProgress progress,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<ZippyshareConfig>();
        var errors = new List<string>();

        foreach (var attempt in Enumerable.Range(1, UploadAttempts))
        {
            try
            {
                logger.LogInformation(
                    "Uploading file {FilePath} to Zippyshare (Attempt {Attempt})",
                    fileDto.FullFileName,
                    attempt
                );

                await using var stream = SequentialFileReader.OpenRead(fileDto.FullFileName);
                var response = await apiClient.UploadFileAsync(
                    config: config,
                    stream: new ProgressReportingStream(stream, progress, stream.Length),
                    fileName: Path.GetFileName(fileDto.FullFileName),
                    cancellationToken: cancellationToken
                );

                return new UploadFileResult(
                    IsSuccess: true,
                    FileDto: fileDto,
                    ErrorMessages: [],
                    FileUrl: response.DownloadLink,
                    ExternalId: response.FileId
                );
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is not HosterCredentialsRejectedException)
            {
                var message = ex.InnerException?.Message ?? ex.Message;
                logger.LogError(
                    ex,
                    "Error while uploading file {FilePath} to Zippyshare on attempt {Attempt}: {ErrorMessage}",
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
        var fileIdPerFileUrl = files
            .DistinctBy(file => file.Url)
            .Select(file => new
            {
                file.Url,
                FileId = file.ExternalId is { Length: > 0 }
                    ? file.ExternalId
                    : ExtractFileIdFromFileLink(file.Url),
            })
            .Where(entry => entry.FileId is not null)
            .ToDictionary(entry => entry.Url, entry => entry.FileId!);

        try
        {
            var fileStatusPerFileId = await apiClient.GetFileStatusesAsync(
                fileIdPerFileUrl.Values.Distinct().ToList(),
                cancellationToken
            );

            var fileStatusPerFileUrl = fileIdPerFileUrl.ToDictionary(
                entry => entry.Key,
                entry => fileStatusPerFileId[entry.Value]
            );

            var statusPerFileUrl = fileStatusPerFileUrl.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.IsOnline
            );

            var downloadCountPerFileUrl = fileStatusPerFileUrl
                .Where(entry => entry.Value.DownloadCount is not null)
                .ToDictionary(entry => entry.Key, entry => entry.Value.DownloadCount!.Value);

            return new FileExistResult(
                IsSuccess: true,
                ErrorMessages: [],
                StatusPerFileUrl: statusPerFileUrl,
                DownloadCountPerFileUrl: downloadCountPerFileUrl
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new FileExistResult(
                IsSuccess: false,
                ErrorMessages: [ex.InnerException?.Message ?? ex.Message],
                StatusPerFileUrl: new Dictionary<string, bool>()
            );
        }
    }

    public async Task<FileInfoResult> GetFileInfosAsync(
        IHosterConfig hosterConfig,
        IReadOnlyList<string> fileLinks,
        CancellationToken cancellationToken
    )
    {
        var fileIdPerFileLink = FileInfoResultMapping.GetFileIdPerFileLink(
            fileLinks,
            ExtractFileIdFromFileLink
        );

        try
        {
            var fileStatusPerFileId = await apiClient.GetFileStatusesAsync(
                fileIdPerFileLink.Values.Distinct().ToList(),
                cancellationToken
            );

            var fileInfoPerFileId = fileStatusPerFileId.ToDictionary(
                entry => entry.Key,
                entry => new HostedFileInfo(
                    IsOnline: entry.Value.IsOnline,
                    FileName: entry.Value.FileName,
                    SizeBytes: entry.Value.SizeBytes
                )
            );

            return FileInfoResultMapping.CreateResult(fileIdPerFileLink, fileInfoPerFileId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return FileInfoResultMapping.CreateFailedResult(ex);
        }
    }

    public string? ConvertToOwnFileLinkFormat(string fileLink)
    {
        return ExtractFileIdFromFileLink(fileLink) is { } fileId
            ? $"https://zippyshare.day/f/{fileId}"
            : null;
    }

    public IHosterConfig DeserializeHosterConfig(string serializedConfig)
    {
        var config = JsonSerializer.Deserialize<ZippyshareConfig>(serializedConfig);

        return config
            ?? throw new InvalidOperationException("Failed to deserialize Zippyshare config");
    }

    public string SerializeHosterConfig(Dictionary<string, string> hosterConfig)
    {
        var config = new ZippyshareConfig
        {
            ApiKey =
                hosterConfig.GetValueOrDefault(nameof(ZippyshareConfig.ApiKey)) ?? string.Empty,
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

    public async Task<HosterStorageQuota> GetStorageQuotaAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var user = await apiClient.GetUserAsync(
            config: hosterConfig.As<ZippyshareConfig>(),
            cancellationToken: cancellationToken
        );

        return user.Limits.StorageRemaining is { } storageRemaining
            ? HosterStorageQuota.Limited(
                freeBytes: storageRemaining,
                totalBytes: user.Limits.StorageTotal
            )
            : HosterStorageQuota.Unlimited();
    }

    public async Task<TryLoginResult> TryLoginAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<ZippyshareConfig>();

        try
        {
            await apiClient.GetUserAsync(config, cancellationToken);

            return new TryLoginResult(IsSuccess: true, ErrorMessage: null);
        }
        catch (Exception ex)
        {
            return new TryLoginResult(
                IsSuccess: false,
                ErrorMessage: ex.InnerException?.Message ?? ex.Message
            );
        }
    }

    private string? ExtractFileIdFromFileLink(string fileLink)
    {
        var fileLinkUri = FileLinkHostMatching.ParseFileLinkWithAnyHostName(
            fileLink,
            FileLinkHostNames
        );

        if (fileLinkUri is null)
        {
            return null;
        }

        return
            FileLinkHostMatching.GetPathSegments(fileLinkUri) is ["f" or "p", var fileId, ..]
            && fileId.All(char.IsAsciiLetterOrDigit)
            ? fileId
            : null;
    }
}
