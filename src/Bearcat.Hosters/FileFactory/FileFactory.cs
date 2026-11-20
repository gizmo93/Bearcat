using System.Text.Json;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Abstractions.Transfers;
using Bearcat.Hosters.Extensions;
using Bearcat.Hosters.FileFactory.Api;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.FileFactory;

public class FileFactory(IFileFactoryApiClient apiClient, ILogger<FileFactory> logger)
    : IHosterWithBatchFileMove,
        IHosterWithFileInfo,
        IHosterWithFileSizeLimit
{
    private const string FileLinkPathSegment = "file";

    private const int FileIdLength = 32;

    private const int MaxParallelUploads = 3;

    private const int UploadAttempts = 3;

    public string Name => "FileFactory";

    public IReadOnlyList<string> FileLinkHostNames => ["filefactory.com"];

    public bool SupportsPremiumOnlyDownloads => false;

    public IReadOnlyList<string> ConfigurationKeys =>
        [nameof(FileFactoryConfig.Email), nameof(FileFactoryConfig.Password)];

    public TimeSpan UploadRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    public bool HasFixedParallelUploadLimit => false;

    public int? DefaultMaximumParallelUploads => MaxParallelUploads;

    public int MaxFileSizeMb => 2147;

    public async Task<UploadFileResult> UploadFileAsync(
        FileDto fileDto,
        IHosterConfig hosterConfig,
        ITransferProgress progress,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<FileFactoryConfig>();
        var errors = new List<string>();
        var fileInfo = new FileInfo(fileDto.FullFileName);

        foreach (var attempt in Enumerable.Range(1, UploadAttempts))
        {
            try
            {
                logger.LogInformation(
                    "Uploading file {FilePath} to FileFactory (Attempt {Attempt})",
                    fileDto.FullFileName,
                    attempt
                );

                await using var stream = SequentialFileReader.OpenRead(fileDto.FullFileName);
                var uploadResult = await apiClient.UploadFileAsync(
                    config: config,
                    stream: new ThrottledProgressReportingStream(stream, progress, stream.Length),
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
            catch (Exception ex) when (ex is not HosterCredentialsRejectedException)
            {
                var message = ex.InnerException?.Message ?? ex.Message;
                logger.LogError(
                    ex,
                    "Error while uploading file {FilePath} to FileFactory on attempt {Attempt}: {ErrorMessage}",
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
        var config = hosterConfig.As<FileFactoryConfig>();
        var fileIdPerFileUrl = FileInfoResultMapping.GetFileIdPerFileLink(
            files.Select(file => file.Url).ToList(),
            ExtractFileIdFromFileLink
        );

        try
        {
            var fileInfoPerFileId = await apiClient.GetFileInfoPerFileIdAsync(
                config: config,
                fileIds: fileIdPerFileUrl.Values.ToList(),
                cancellationToken: cancellationToken
            );

            var statusPerFileUrl = fileIdPerFileUrl.ToDictionary(
                entry => entry.Key,
                entry => fileInfoPerFileId[entry.Value].IsOnline
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
        var config = hosterConfig.As<FileFactoryConfig>();
        var fileIdPerFileLink = FileInfoResultMapping.GetFileIdPerFileLink(
            fileLinks,
            ExtractFileIdFromFileLink
        );

        try
        {
            var fileInfoPerFileId = await apiClient.GetFileInfoPerFileIdAsync(
                config: config,
                fileIds: fileIdPerFileLink.Values.ToList(),
                cancellationToken: cancellationToken
            );

            return FileInfoResultMapping.CreateResult(fileIdPerFileLink, fileInfoPerFileId);
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
        var config = hosterConfig.As<FileFactoryConfig>();

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
        var config = hosterConfig.As<FileFactoryConfig>();

        if (string.IsNullOrEmpty(externalId))
        {
            throw new HttpRequestException(
                $"Cannot move FileFactory file {fileUrl} because its file id is unknown"
            );
        }

        await apiClient.MoveFilesToFolderAsync(
            config: config,
            fileIds: [externalId],
            folderId: folderId,
            cancellationToken: cancellationToken
        );
    }

    public async Task<IReadOnlyList<string>> MoveFilesToFolderAsync(
        IReadOnlyList<HosterFileToMoveDto> files,
        string folderId,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<FileFactoryConfig>();
        var movableFiles = new List<(string FileLink, string FileId)>();

        foreach (var file in files)
        {
            if (string.IsNullOrEmpty(file.ExternalId))
            {
                logger.LogWarning(
                    "Cannot move FileFactory file {FileLink} because its file id is unknown",
                    file.HosterFileLink
                );

                continue;
            }

            movableFiles.Add((file.HosterFileLink, file.ExternalId));
        }

        if (movableFiles.Count == 0)
        {
            return [];
        }

        try
        {
            await apiClient.MoveFilesToFolderAsync(
                config: config,
                fileIds: movableFiles.Select(file => file.FileId).ToList(),
                folderId: folderId,
                cancellationToken: cancellationToken
            );

            return movableFiles.Select(file => file.FileLink).ToList();
        }
        catch (Exception ex)
            when (ex is not HosterCredentialsRejectedException and not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Failed to move {FileCount} FileFactory files to folder {FolderId}",
                movableFiles.Count,
                folderId
            );

            return [];
        }
    }

    public string? ConvertToOwnFileLinkFormat(string fileLink)
    {
        return ExtractFileIdFromFileLink(fileLink) is { } fileId
            ? $"{ApiClient.FileLinkBaseUrl}{fileId}"
            : null;
    }

    public IHosterConfig DeserializeHosterConfig(string serializedConfig)
    {
        var config = JsonSerializer.Deserialize<FileFactoryConfig>(serializedConfig);

        return config
            ?? throw new InvalidOperationException("Failed to deserialize FileFactory config");
    }

    public string SerializeHosterConfig(Dictionary<string, string> hosterConfig)
    {
        var config = new FileFactoryConfig
        {
            Email = hosterConfig.GetValueOrDefault(nameof(FileFactoryConfig.Email)) ?? string.Empty,
            Password =
                hosterConfig.GetValueOrDefault(nameof(FileFactoryConfig.Password)) ?? string.Empty,
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
        var config = hosterConfig.As<FileFactoryConfig>();

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
            FileLinkHostMatching.GetPathSegments(fileLinkUri)
                is [FileLinkPathSegment, var fileId, ..]
            && fileId.Length == FileIdLength
            && fileId.All(char.IsAsciiHexDigit)
            ? fileId
            : null;
    }
}
