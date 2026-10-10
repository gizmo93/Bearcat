using Bearcat.Abstractions;
using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageReleases;

public class ReleaseFolderRetirementService(
    IReleaseFolderRetirementRepository repository,
    IApplicationConfigurationProvider configuration,
    UnmanagedReleaseConverter unmanagedReleaseConverter,
    IFileSystemService fileSystemService,
    IOptions<WorkingDirectoriesConfig> workingDirectoriesConfig,
    INotificationService notificationService,
    TimeProvider timeProvider,
    ILogger<ReleaseFolderRetirementService> logger
)
{
    private const string ConversionReason =
        "The release was converted to unmanaged because its release folder retention expired.";

    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var autoConvertEnabled = configuration.GetValue<ArchiveCleanupConfiguration>(c =>
            c.AutoConvertToUnmanaged
        );

        if (!autoConvertEnabled)
        {
            return;
        }

        var retentionDays = configuration.GetValue<ArchiveCleanupConfiguration>(c =>
            c.ReleaseFolderRetentionDays
        );

        var deleteReleaseFolderOnConversion = configuration.GetValue<ArchiveCleanupConfiguration>(
            c => c.DeleteReleaseFolderOnConversion
        );

        var cutoff = timeProvider.GetLocalNow().AddDays(-retentionDays);

        var candidates = await repository.GetConversionCandidatesAsync(cutoff, cancellationToken);
        var releaseFoldersToDelete = new List<ReleaseFolderToDelete>();
        var convertedReleaseCount = 0;

        foreach (var release in candidates)
        {
            if (!unmanagedReleaseConverter.IsRecoverableWithoutReleaseFolder(release))
            {
                logger.LogDebug(
                    "Release {ReleaseId} stays managed because not every archive config has a local archive or an online mirror",
                    release.Id
                );

                continue;
            }

            var releaseFolderPath = release.ReleaseFolderPath!;

            var keptReleaseFolderMessage = GetKeptReleaseFolderMessage(
                release: release,
                releaseFolderPath: releaseFolderPath,
                deleteReleaseFolderOnConversion: deleteReleaseFolderOnConversion
            );

            UnmanagedReleaseConverter.ConvertToUnmanaged(release);
            convertedReleaseCount++;

            if (keptReleaseFolderMessage is null)
            {
                releaseFoldersToDelete.Add(new ReleaseFolderToDelete(release, releaseFolderPath));

                continue;
            }

            logger.LogInformation(
                "Converting release {ReleaseId} to unmanaged and keeping its release folder {ReleaseFolderPath}",
                release.Id,
                releaseFolderPath
            );

            CreateNotification(release, keptReleaseFolderMessage);
        }

        if (convertedReleaseCount == 0)
        {
            return;
        }

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Converted {ReleaseCount} releases to unmanaged because their release folder retention expired",
            convertedReleaseCount
        );

        if (releaseFoldersToDelete.Count == 0)
        {
            return;
        }

        foreach (var releaseFolderToDelete in releaseFoldersToDelete)
        {
            DeleteReleaseFolder(releaseFolderToDelete);
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    private string? GetKeptReleaseFolderMessage(
        Release release,
        string releaseFolderPath,
        bool deleteReleaseFolderOnConversion
    )
    {
        var hasArchivesInsideReleaseFolder = UnmanagedReleaseConverter.HasCreatedArchiveInside(
            release,
            releaseFolderPath
        );

        if (!deleteReleaseFolderOnConversion)
        {
            return hasArchivesInsideReleaseFolder
                ? $"{ConversionReason} The archives of this release are stored inside {releaseFolderPath}, so delete only the release data there and keep the archive files."
                : $"{ConversionReason} You can now delete the release folder yourself: {releaseFolderPath}.";
        }

        if (IsDriveRootOrContainsWorkingDirectory(releaseFolderPath))
        {
            return $"{ConversionReason} The release folder {releaseFolderPath} was kept because it is a drive root or contains a working directory. Delete the release data there yourself.";
        }

        if (hasArchivesInsideReleaseFolder)
        {
            return $"{ConversionReason} The release folder {releaseFolderPath} was kept because the archives of this release are stored inside it. Delete only the release data there and keep the archive files.";
        }

        if (!HasCompleteLocalArchiveForEveryArchiveConfig(release))
        {
            return $"{ConversionReason} The release folder {releaseFolderPath} was kept because not every archive config has a local archive with all archive files on disk. You can delete it yourself.";
        }

        return null;
    }

    private bool IsDriveRootOrContainsWorkingDirectory(string releaseFolderPath)
    {
        return FolderPathHelper.IsFileSystemRoot(releaseFolderPath)
            || workingDirectoriesConfig
                .Value.GetWorkingDirectories()
                .Any(workingDirectory =>
                    FolderPathHelper.IsSameOrSubPath(
                        childPath: workingDirectory,
                        parentPath: releaseFolderPath
                    )
                );
    }

    private bool HasCompleteLocalArchiveForEveryArchiveConfig(Release release)
    {
        return release.ArchiveConfigs.All(config =>
            config.Archives.Any(archive =>
                archive.ArchiveState is ArchiveState.Created
                && archive.ArchiveFiles.All(archiveFile =>
                    fileSystemService.FileExists(archiveFile.FullFileName)
                )
            )
        );
    }

    private void DeleteReleaseFolder(ReleaseFolderToDelete releaseFolderToDelete)
    {
        var (release, releaseFolderPath) = releaseFolderToDelete;

        try
        {
            fileSystemService.DeleteDirectoryIfExists(releaseFolderPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                exception,
                "Could not delete release folder {ReleaseFolderPath} of release {ReleaseId} after converting it to unmanaged",
                releaseFolderPath,
                release.Id
            );

            CreateNotification(
                release,
                $"{ConversionReason} Its release folder {releaseFolderPath} could not be deleted, so delete it yourself."
            );

            return;
        }

        logger.LogInformation(
            "Deleted release folder {ReleaseFolderPath} of release {ReleaseId} after converting it to unmanaged",
            releaseFolderPath,
            release.Id
        );

        CreateNotification(
            release,
            $"{ConversionReason} Its release folder {releaseFolderPath} was deleted."
        );
    }

    private void CreateNotification(Release release, string message)
    {
        notificationService.Create(
            kind: NotificationKind.ReleaseAutoConvertedToUnmanaged,
            message: message,
            entity: release,
            selector: n => n.Release
        );
    }

    private sealed record ReleaseFolderToDelete(Release Release, string ReleaseFolderPath);
}
