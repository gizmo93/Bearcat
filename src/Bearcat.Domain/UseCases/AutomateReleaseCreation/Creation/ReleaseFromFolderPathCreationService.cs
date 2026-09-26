using Bearcat.Abstractions;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.Creation.Repositories;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.Exceptions;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.FolderUsage;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.FolderUsage.Repositories;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.Creation;

public class ReleaseFromFolderPathCreationService(
    IReleaseFromFolderPathCreationRepository repository,
    IReleaseFolderUsageRepository releaseFolderUsageRepository,
    IFileSystemService fileSystemService,
    ReleaseFromFolderCreationService releaseFromFolderCreationService,
    TimeProvider timeProvider,
    INotificationService notificationService
)
{
    public async Task<int> CreateAsync(
        string folderPath,
        int releaseTemplateId,
        string? name,
        string? primaryLanguageCode,
        CancellationToken cancellationToken
    )
    {
        var normalizedFolderPath = Path.TrimEndingDirectorySeparator(folderPath.Trim());

        if (!fileSystemService.DirectoryExists(normalizedFolderPath))
        {
            throw new ReleaseFolderNotFoundException(normalizedFolderPath);
        }

        var releaseTemplate =
            await repository.GetTemplateForReleaseCreationOrDefaultAsync(
                releaseTemplateId,
                cancellationToken
            ) ?? throw new ReleaseTemplateNotFoundException(releaseTemplateId);

        var usageKind = await GetFolderUsageKindAsync(normalizedFolderPath, cancellationToken);

        if (usageKind is not null)
        {
            throw new ReleaseFolderAlreadyInUseException(normalizedFolderPath, usageKind.Value);
        }

        var release = await releaseFromFolderCreationService.CreateAsync(
            releaseTemplate: releaseTemplate,
            folderPath: normalizedFolderPath,
            name: name,
            primaryLanguageCode: NormalizePrimaryLanguageCode(primaryLanguageCode),
            localNow: timeProvider.GetLocalNow(),
            cancellationToken: cancellationToken
        );

        repository.Add(release);

        notificationService.Create(
            kind: NotificationKind.ReleaseAutomaticallyCreated,
            message: $"Release '{release.Name}' was created through the API from template '{releaseTemplate.Name}'",
            entity: release,
            selector: notification => notification.Release
        );

        await repository.SaveChangesAsync(cancellationToken);

        return release.Id;
    }

    private async Task<ReleaseFolderUsageKind?> GetFolderUsageKindAsync(
        string folderPath,
        CancellationToken cancellationToken
    )
    {
        List<string> folderPaths = [folderPath];

        var releaseFolderPaths =
            await releaseFolderUsageRepository.GetExistingReleaseFolderPathsAsync(
                folderPaths,
                cancellationToken
            );

        if (releaseFolderPaths.Count > 0)
        {
            return ReleaseFolderUsageKind.ReleaseFolder;
        }

        var archiveFolderPaths =
            await releaseFolderUsageRepository.GetExistingArchiveFolderPathsAsync(
                folderPaths,
                cancellationToken
            );

        if (archiveFolderPaths.Count > 0)
        {
            return ReleaseFolderUsageKind.UnmanagedArchiveFolder;
        }

        var remoteDownloadFolderPaths =
            await releaseFolderUsageRepository.GetRemoteDownloadFolderPathsAsync(
                folderPaths,
                cancellationToken
            );

        if (remoteDownloadFolderPaths.Count > 0)
        {
            return ReleaseFolderUsageKind.RemoteDownloadFolder;
        }

        return null;
    }

    private static string? NormalizePrimaryLanguageCode(string? primaryLanguageCode)
    {
        return string.IsNullOrWhiteSpace(primaryLanguageCode)
            ? null
            : primaryLanguageCode.Trim().ToLowerInvariant();
    }
}
