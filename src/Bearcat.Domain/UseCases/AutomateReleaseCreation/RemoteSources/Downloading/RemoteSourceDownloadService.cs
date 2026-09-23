using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.RemoteSource;
using Bearcat.Abstractions.RemoteSource.Dto;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Downloading.Repositories;
using Bearcat.Domain.UseCases.ManageRemoteSources.Sessions;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Downloading;

public class RemoteSourceDownloadService(
    IRemoteSourceDownloadRepository repository,
    RemoteSourceSessionProvider sessionProvider,
    RemoteDownloadFolderService folderService,
    ITransferProgressTracker progressTracker,
    ITransferCancellationRegistry cancellationRegistry,
    INotificationService notificationService,
    IApplicationConfigurationProvider configuration,
    TimeProvider timeProvider,
    ILogger<RemoteSourceDownloadService> logger
)
{
    public static TransferKey CreateTransferKey(int downloadId)
    {
        return new TransferKey(TransferKind.RemoteDownload, downloadId);
    }

    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await ResetInterruptedDownloadsAsync(cancellationToken);

        foreach (var download in await repository.GetPendingDownloadsAsync(cancellationToken))
        {
            await ProcessDownloadAsync(download, cancellationToken);
        }
    }

    private async Task ResetInterruptedDownloadsAsync(CancellationToken cancellationToken)
    {
        foreach (var download in await repository.GetInterruptedDownloadsAsync(cancellationToken))
        {
            logger.LogInformation(
                "Discarding the interrupted remote download {DownloadId} in {LocalFolderPath} and queueing it again",
                download.Id,
                download.LocalFolderPath
            );

            folderService.DeleteDownloadedFiles(download);
            download.State = RemoteSourceDownloadState.Pending;
            download.StartedAt = null;
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessDownloadAsync(
        RemoteSourceDownload download,
        CancellationToken stoppingToken
    )
    {
        if (
            !await repository.TryRefreshAsync(download, stoppingToken)
            || download.State is not RemoteSourceDownloadState.Pending
            || download.RemoteSourceRegistrationId is null
            || download.RemoteSourceRegistration is not { IsActive: true }
        )
        {
            return;
        }

        if (folderService.ContainsData(download))
        {
            await FailAsync(
                download,
                $"The local folder {download.LocalFolderPath} already exists and is not empty. Remove or rename it and restart the download",
                stoppingToken
            );
            return;
        }

        var transferKey = CreateTransferKey(download.Id);
        var userCancellationToken = cancellationRegistry.Register(transferKey);
        IReadOnlyList<RemoteFileDto> files;

        try
        {
            await MarkAsDownloadingAsync(download, stoppingToken);

            using var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
                userCancellationToken,
                stoppingToken
            );
            files = await DownloadFilesAsync(download, transferKey, cancellationSource.Token);
        }
        catch (Exception)
            when (userCancellationToken.IsCancellationRequested
                && !stoppingToken.IsCancellationRequested
            )
        {
            await CancelAsync(download, stoppingToken);
            return;
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            folderService.DeleteDownloadedFiles(download);
            await FailAsync(download, exception.Message, stoppingToken);
            return;
        }
        finally
        {
            cancellationRegistry.Unregister(transferKey);
            progressTracker.StopTracking(transferKey);
        }

        await CompleteAsync(download, files, stoppingToken);
    }

    private async Task MarkAsDownloadingAsync(
        RemoteSourceDownload download,
        CancellationToken cancellationToken
    )
    {
        download.State = RemoteSourceDownloadState.Downloading;
        download.StartedAt = timeProvider.GetLocalNow();
        download.CompletedAt = null;
        download.ErrorMessage = null;
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Starting the remote download {DownloadId} of {RemoteFolderPath} from {RemoteSourceName}",
            download.Id,
            download.RemoteFolderPath,
            download.SourceName
        );
    }

    private async Task<IReadOnlyList<RemoteFileDto>> DownloadFilesAsync(
        RemoteSourceDownload download,
        TransferKey transferKey,
        CancellationToken cancellationToken
    )
    {
        var files = await RunWithRetriesAsync(
            download: download,
            operationName: $"Listing of remote folder '{download.RemoteFolderPath}'",
            operation: session =>
                session.ListFilesRecursiveAsync(download.RemoteFolderPath, cancellationToken),
            cancellationToken: cancellationToken
        );

        if (files.Count == 0)
        {
            throw new InvalidOperationException(
                $"The remote folder '{download.RemoteFolderPath}' contains no files"
            );
        }

        var plannedFiles = files
            .Select(
                (file, index) =>
                    new PlannedFile(index + 1, file, ResolveLocalFilePath(download, file))
            )
            .ToList();

        progressTracker.StartTracking(
            transferKey,
            plannedFiles
                .Select(file => new PlannedTransferFile(
                    FileId: file.FileId,
                    FileName: file.File.RelativePath,
                    SourceName: download.SourceName,
                    SizeBytes: file.File.SizeBytes,
                    IsAlreadyTransferred: false
                ))
                .ToList()
        );

        logger.LogInformation(
            "Downloading {FileCount} files of {RemoteFolderPath} from {RemoteSourceName} into {LocalFolderPath}",
            files.Count,
            download.RemoteFolderPath,
            download.SourceName,
            download.LocalFolderPath
        );

        var remoteSourceConfiguration = configuration.GetConfiguration<RemoteSourceConfiguration>();
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Min(
                Math.Max(1, remoteSourceConfiguration.MaxParallelFileDownloads),
                download.RemoteSourceRegistration!.MaxConnections
            ),
            CancellationToken = cancellationToken,
        };

        await Parallel.ForEachAsync(
            plannedFiles,
            parallelOptions,
            async (file, token) =>
                await DownloadFileAsync(download: download, transferKey, file, token)
        );

        return files;
    }

    private async Task DownloadFileAsync(
        RemoteSourceDownload download,
        TransferKey transferKey,
        PlannedFile file,
        CancellationToken cancellationToken
    )
    {
        var progress = new TransferProgressReporter(
            tracker: progressTracker,
            key: transferKey,
            fileId: file.FileId,
            fileName: file.File.RelativePath,
            sourceName: download.SourceName
        );

        await RunWithRetriesAsync(
            download,
            $"Download of '{file.File.RelativePath}'",
            async session =>
            {
                await session.DownloadFileAsync(
                    file.File,
                    file.LocalFilePath,
                    progress,
                    cancellationToken
                );

                return VerifySize(file);
            },
            cancellationToken
        );
    }

    private async Task<T> RunWithRetriesAsync<T>(
        RemoteSourceDownload download,
        string operationName,
        Func<IRemoteSourceSession, Task<T>> operation,
        CancellationToken cancellationToken
    )
    {
        var remoteSourceConfiguration = configuration.GetConfiguration<RemoteSourceConfiguration>();
        var maxAttempts = Math.Max(1, remoteSourceConfiguration.MaxDownloadAttempts);
        var retryDelay = TimeSpan.FromSeconds(
            Math.Max(0, remoteSourceConfiguration.DownloadRetryDelaySeconds)
        );
        var errorMessages = new List<string>();

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (attempt > 1)
            {
                await Task.Delay(retryDelay, cancellationToken);
            }

            try
            {
                return await sessionProvider.UseSessionAsync(
                    download.RemoteSourceRegistration!,
                    operation,
                    cancellationToken
                );
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                errorMessages.Add(exception.Message);

                logger.LogWarning(
                    exception,
                    "{OperationName} for remote download {DownloadId} from {RemoteSourceName} failed on attempt {Attempt} of {MaxAttempts}",
                    operationName,
                    download.Id,
                    download.SourceName,
                    attempt,
                    maxAttempts
                );
            }
        }

        throw new IOException(
            $"{operationName} failed after {maxAttempts} attempts: {string.Join(" | ", errorMessages.Distinct())}"
        );
    }

    private async Task CompleteAsync(
        RemoteSourceDownload download,
        IReadOnlyList<RemoteFileDto> files,
        CancellationToken cancellationToken
    )
    {
        download.State = RemoteSourceDownloadState.Downloaded;
        download.CompletedAt = timeProvider.GetLocalNow();
        download.FileCount = files.Count;
        download.TotalBytes = files.Sum(file => file.SizeBytes);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Downloaded {FileCount} files of {RemoteFolderPath} from {RemoteSourceName} into {LocalFolderPath}",
            download.FileCount,
            download.RemoteFolderPath,
            download.SourceName,
            download.LocalFolderPath
        );
    }

    private async Task CancelAsync(
        RemoteSourceDownload download,
        CancellationToken cancellationToken
    )
    {
        folderService.DeleteDownloadedFiles(download);
        download.State = RemoteSourceDownloadState.Canceled;
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Canceled the remote download {DownloadId} on user request and discarded {LocalFolderPath}",
            download.Id,
            download.LocalFolderPath
        );
    }

    private async Task FailAsync(
        RemoteSourceDownload download,
        string errorMessage,
        CancellationToken cancellationToken
    )
    {
        download.State = RemoteSourceDownloadState.Failed;
        download.ErrorMessage = errorMessage;

        await notificationService.CreateAsync(
            NotificationKind.RemoteDownloadFailed,
            $"Remote download '{download.FolderName}' from {download.SourceName} failed: {errorMessage}",
            cancellationToken
        );
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogError(
            "Remote download {DownloadId} of {RemoteFolderPath} from {RemoteSourceName} failed: {ErrorMessage}",
            download.Id,
            download.RemoteFolderPath,
            download.SourceName,
            errorMessage
        );
    }

    private static string ResolveLocalFilePath(RemoteSourceDownload download, RemoteFileDto file)
    {
        return RemoteDownloadPaths.ResolveLocalFilePath(download.LocalFolderPath, file.RelativePath)
            ?? throw new InvalidOperationException(
                $"The remote source listed the unsafe file path '{file.RelativePath}', so nothing was downloaded"
            );
    }

    private static long VerifySize(PlannedFile file)
    {
        var actualSizeBytes = new FileInfo(file.LocalFilePath).Length;

        if (actualSizeBytes != file.File.SizeBytes)
        {
            throw new IOException(
                $"The downloaded file has {actualSizeBytes} bytes but the remote source listed {file.File.SizeBytes} bytes"
            );
        }

        return actualSizeBytes;
    }

    private sealed record PlannedFile(int FileId, RemoteFileDto File, string LocalFilePath);
}
