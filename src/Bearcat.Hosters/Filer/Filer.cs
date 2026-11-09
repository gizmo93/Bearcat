using System.Text.Json;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Abstractions.Transfers;
using Bearcat.Hosters.Extensions;
using Bearcat.Hosters.Filer.Api;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Filer;

public class Filer(IFilerApiClient apiClient, ILogger<Filer> logger)
    : IHosterWithFolders,
        IHosterWithFileInfo,
        IHosterWithDownload
{
    private const char ExternalIdSeparator = ':';

    private const string FileLinkPathSegment = "get";

    private const int MaxParallelUploads = 3;

    private const int UploadAttempts = 3;

    public string Name => "Filer.net";

    public IReadOnlyList<string> FileLinkHostNames => ["filer.net"];

    public bool SupportsPremiumOnlyDownloads => false;

    public bool DownloadRequiresPremium => true;

    public IReadOnlyList<string> ConfigurationKeys =>
        [nameof(FilerConfig.Email), nameof(FilerConfig.Password)];

    public TimeSpan UploadRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    public bool HasFixedParallelUploadLimit => false;

    public int? DefaultMaximumParallelUploads => MaxParallelUploads;

    public async Task<UploadFileResult> UploadFileAsync(
        FileDto fileDto,
        IHosterConfig hosterConfig,
        ITransferProgress progress,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<FilerConfig>();
        var errors = new List<string>();
        var fileInfo = new FileInfo(fileDto.FullFileName);

        foreach (var attempt in Enumerable.Range(1, UploadAttempts))
        {
            try
            {
                logger.LogInformation(
                    "Uploading file {FilePath} to Filer (Attempt {Attempt})",
                    fileDto.FullFileName,
                    attempt
                );

                await using var stream = SequentialFileReader.OpenRead(fileDto.FullFileName);
                var uploadResult = await apiClient.UploadFileAsync(
                    config: config,
                    stream: new ProgressReportingStream(stream, progress, stream.Length),
                    fileName: fileInfo.Name,
                    fileSize: fileInfo.Length,
                    folderHash: fileDto.FolderId,
                    cancellationToken: cancellationToken
                );

                return new UploadFileResult(
                    IsSuccess: true,
                    FileDto: fileDto,
                    ErrorMessages: [],
                    FileUrl: uploadResult.FileUrl,
                    ExternalId: $"{uploadResult.FileHash}{ExternalIdSeparator}{uploadResult.EditCode}"
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
                    "Error while uploading file {FilePath} to Filer on attempt {Attempt}: {ErrorMessage}",
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
        var fileHashPerFileUrl = FileInfoResultMapping.GetFileIdPerFileLink(
            files.Select(file => file.Url).ToList(),
            ExtractFileHashFromFileLink
        );

        try
        {
            var fileInfoPerFileHash = await apiClient.GetFileInfoPerFileHashAsync(
                fileHashPerFileUrl.Values.ToList(),
                cancellationToken
            );

            var statusPerFileUrl = fileHashPerFileUrl.ToDictionary(
                entry => entry.Key,
                entry => fileInfoPerFileHash[entry.Value].IsOnline
            );

            return new FileExistResult(
                IsSuccess: true,
                ErrorMessages: [],
                StatusPerFileUrl: statusPerFileUrl
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
        var fileHashPerFileLink = FileInfoResultMapping.GetFileIdPerFileLink(
            fileLinks,
            ExtractFileHashFromFileLink
        );

        try
        {
            var fileInfoPerFileHash = await apiClient.GetFileInfoPerFileHashAsync(
                fileHashPerFileLink.Values.ToList(),
                cancellationToken
            );

            return FileInfoResultMapping.CreateResult(fileHashPerFileLink, fileInfoPerFileHash);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return FileInfoResultMapping.CreateFailedResult(ex);
        }
    }

    public async Task<string> CreateFolderAsync(
        string folderName,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<FilerConfig>();

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
        var config = hosterConfig.As<FilerConfig>();

        if (
            externalId?.Split(ExternalIdSeparator)
            is not [{ Length: > 0 } fileHash, { Length: > 0 } editCode]
        )
        {
            throw new HttpRequestException(
                $"Cannot move Filer file {fileUrl} because its edit code is unknown"
            );
        }

        await apiClient.MoveFileToFolderAsync(
            config: config,
            fileHash: fileHash,
            editCode: editCode,
            folderHash: folderId,
            cancellationToken: cancellationToken
        );
    }

    public async Task<DownloadFileResult> DownloadFileAsync(
        DownloadFileDto file,
        string targetFilePath,
        IHosterConfig hosterConfig,
        ITransferProgress progress,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<FilerConfig>();

        try
        {
            var fileHash =
                ExtractFileHashFromFileLink(file.HosterFileLink)
                ?? throw new HttpRequestException(
                    $"Could not extract Filer file hash from URL {file.HosterFileLink}"
                );

            await apiClient.DownloadFileAsync(
                config: config,
                fileHash: fileHash,
                targetFilePath: targetFilePath,
                progress: progress,
                expectedSizeBytes: file.ExpectedSizeBytes,
                cancellationToken: cancellationToken
            );

            return new DownloadFileResult(IsSuccess: true, ErrorMessages: []);
        }
        catch (Exception ex)
            when (ex is not (OperationCanceledException or HosterCredentialsRejectedException))
        {
            logger.LogError(
                ex,
                "Failed to download file {FileLink} from Filer: {Message}",
                file.HosterFileLink,
                ex.InnerException?.Message ?? ex.Message
            );

            return DownloadFailure.FromException(ex);
        }
    }

    public async Task<IReadOnlyDictionary<string, long>> GetFileSizesAsync(
        IReadOnlyList<string> fileUrls,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var fileHashPerFileUrl = FileInfoResultMapping.GetFileIdPerFileLink(
            fileUrls,
            ExtractFileHashFromFileLink
        );

        try
        {
            var fileInfoPerFileHash = await apiClient.GetFileInfoPerFileHashAsync(
                fileHashPerFileUrl.Values.ToList(),
                cancellationToken
            );

            return fileHashPerFileUrl
                .Where(entry => fileInfoPerFileHash[entry.Value].SizeBytes is not null)
                .ToDictionary(
                    entry => entry.Key,
                    entry => fileInfoPerFileHash[entry.Value].SizeBytes!.Value
                );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to look up file sizes on Filer: {Message}",
                ex.InnerException?.Message ?? ex.Message
            );

            return new Dictionary<string, long>();
        }
    }

    public string? ConvertToOwnFileLinkFormat(string fileLink)
    {
        return ExtractFileHashFromFileLink(fileLink) is { } fileHash
            ? $"https://filer.net/get/{fileHash}"
            : null;
    }

    public IHosterConfig DeserializeHosterConfig(string serializedConfig)
    {
        var config = JsonSerializer.Deserialize<FilerConfig>(serializedConfig);

        return config ?? throw new InvalidOperationException("Failed to deserialize Filer config");
    }

    public string SerializeHosterConfig(Dictionary<string, string> hosterConfig)
    {
        var config = new FilerConfig
        {
            Email = hosterConfig.GetValueOrDefault(nameof(FilerConfig.Email)) ?? string.Empty,
            Password = hosterConfig.GetValueOrDefault(nameof(FilerConfig.Password)) ?? string.Empty,
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

    public async Task<TryLoginResult> TryLoginAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<FilerConfig>();

        try
        {
            await apiClient.EnsureCredentialsAcceptedAsync(config, cancellationToken);

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

    private string? ExtractFileHashFromFileLink(string fileLink)
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
            FileLinkHostMatching.GetPathSegments(fileLinkUri)
                is [FileLinkPathSegment, var fileHash, ..]
            && fileHash.All(char.IsAsciiLetterOrDigit)
            ? fileHash
            : null;
    }
}
