using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.Creation;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.ReleaseCreation.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.ReleaseCreation;

public class RemoteDownloadReleaseCreator(
    IRemoteDownloadReleaseRepository repository,
    ReleaseFromFolderCreator releaseFromFolderCreator,
    INotificationService notificationService,
    TimeProvider timeProvider,
    ILogger<RemoteDownloadReleaseCreator> logger
)
{
    public async Task<int> ProcessAsync(CancellationToken cancellationToken)
    {
        var downloads = await repository.GetDownloadedWithoutReleaseAsync(cancellationToken);
        var createdCount = 0;

        foreach (var download in downloads)
        {
            if (await CreateReleaseAsync(download, cancellationToken))
            {
                createdCount++;
            }
        }

        return createdCount;
    }

    private async Task<bool> CreateReleaseAsync(
        RemoteSourceDownload download,
        CancellationToken cancellationToken
    )
    {
        var releaseTemplate = download.ReleaseTemplateId is { } releaseTemplateId
            ? await repository.GetReleaseTemplateAsync(releaseTemplateId, cancellationToken)
            : null;

        if (releaseTemplate is null)
        {
            await MarkAsFailedAndNotifyAsync(
                download,
                "The release template of the automation was deleted, so no release could be created",
                cancellationToken
            );

            return false;
        }

        Release release;

        try
        {
            release = await releaseFromFolderCreator.CreateAsync(
                releaseTemplate: releaseTemplate,
                folderPath: download.LocalFolderPath,
                primaryLanguageCode: download.PrimaryLanguageCode,
                localNow: timeProvider.GetLocalNow(),
                cancellationToken: cancellationToken
            );
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Could not create a release from remote download {DownloadId} in {LocalFolderPath}",
                download.Id,
                download.LocalFolderPath
            );

            repository.DiscardPendingChanges();
            await MarkAsFailedAndNotifyAsync(
                download,
                $"No release could be created from template '{releaseTemplate.Name}': {exception.Message}",
                cancellationToken
            );

            return false;
        }

        repository.Add(release);
        download.Release = release;
        download.State = RemoteSourceDownloadState.ReleaseCreated;

        notificationService.Create(
            kind: NotificationKind.ReleaseCreatedFromRemoteDownload,
            message: $"Release '{release.Name}' was created automatically from '{download.RemoteFolderPath}' on remote source '{download.SourceName}' using template '{releaseTemplate.Name}'",
            entity: release,
            selector: notification => notification.Release
        );

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created release {ReleaseName} from remote download {DownloadId} in {LocalFolderPath}",
            release.Name,
            download.Id,
            download.LocalFolderPath
        );

        return true;
    }

    private async Task MarkAsFailedAndNotifyAsync(
        RemoteSourceDownload download,
        string errorMessage,
        CancellationToken cancellationToken
    )
    {
        var message =
            $"{errorMessage.TrimEnd('.')}. The downloaded files were kept in {download.LocalFolderPath}.";

        download.State = RemoteSourceDownloadState.Failed;
        download.ErrorMessage = message;

        await notificationService.CreateAsync(
            NotificationKind.RemoteDownloadFailed,
            $"Remote download '{download.FolderName}' from {download.SourceName} failed: {message}",
            cancellationToken
        );
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Remote download {DownloadId} failed: {ErrorMessage}",
            download.Id,
            message
        );
    }
}
