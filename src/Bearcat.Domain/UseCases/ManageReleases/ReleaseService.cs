using Bearcat.Abstractions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ArchiveRetention;
using Bearcat.Domain.Shared.UnmanagedReleases;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.Creation;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.FolderUsage;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.FolderUsage.Repositories;
using Bearcat.Domain.UseCases.ManageReleases.Exceptions;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageReleases;

public class ReleaseService(
    IReleaseWriteRepository writeRepository,
    TimeProvider timeProvider,
    UnmanagedReleaseArchiveInitializationService unmanagedReleaseArchiveInitializationService,
    IFileSystemService fileSystemService,
    IReleaseFolderUsageRepository releaseFolderUsageRepository,
    ReleaseFromFolderCreationService releaseFromFolderCreationService,
    MirrorCoverageEvaluator mirrorCoverageEvaluator,
    LocalArchiveDeleter localArchiveDeleter
)
{
    public async Task<int> CreateAsync(
        string name,
        string releaseFolderPath,
        ReleaseType releaseType,
        ReleaseContentType releaseContentType,
        int releaseGroupId,
        string? primaryLanguageCode,
        bool excludeFromAutoCleanup = false,
        CancellationToken cancellationToken = default
    )
    {
        var localNow = timeProvider.GetLocalNow();
        var isUnmanaged = releaseType is ReleaseType.Unmanaged;

        var release = new Release
        {
            Name = name,
            CreatedAt = localNow,
            ReleaseType = releaseType,
            ReleaseContentType = releaseContentType,
            PrimaryLanguageCode = CleanOptional(primaryLanguageCode)?.ToLowerInvariant(),
            ReleaseGroupId = releaseGroupId,
            ReleaseFolderPath = isUnmanaged ? null : releaseFolderPath,
            ExcludeFromAutoCleanup = excludeFromAutoCleanup,
            ArchiveConfigs = [],
            UploadConfigs = [],
            ImageUploadConfigs = [],
        };

        if (isUnmanaged)
        {
            release.ArchiveConfigs.Add(
                unmanagedReleaseArchiveInitializationService.CreateArchiveConfig(
                    release: release,
                    archiveFolderPath: releaseFolderPath,
                    createdAt: localNow
                )
            );
        }

        writeRepository.Add(release);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return release.Id;
    }

    public async Task UpdateAsync(
        int releaseId,
        string name,
        string? releaseFolderPath,
        ReleaseContentType releaseContentType,
        int releaseGroupId,
        string? primaryLanguageCode,
        bool excludeFromAutoCleanup = false,
        CancellationToken cancellationToken = default
    )
    {
        var release = await writeRepository.GetByIdAsync(releaseId, cancellationToken);

        if (release.ReleaseType is ReleaseType.Managed)
        {
            release.ReleaseFolderPath = releaseFolderPath;
        }

        release.Name = name;
        release.ReleaseContentType = releaseContentType;
        release.ReleaseGroupId = releaseGroupId;
        release.PrimaryLanguageCode = CleanOptional(primaryLanguageCode)?.ToLowerInvariant();
        release.ExcludeFromAutoCleanup = excludeFromAutoCleanup;

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateReleaseGroupAsync(
        IReadOnlyList<int> releaseIds,
        int releaseGroupId,
        CancellationToken cancellationToken = default
    )
    {
        if (releaseIds.Count == 0)
        {
            return;
        }

        var releases = await writeRepository.GetByIdsAsync(releaseIds, cancellationToken);

        foreach (var release in releases)
        {
            release.ReleaseGroupId = releaseGroupId;
        }

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePrimaryLanguageAsync(
        IReadOnlyList<int> releaseIds,
        string? primaryLanguageCode,
        CancellationToken cancellationToken = default
    )
    {
        if (releaseIds.Count == 0)
        {
            return;
        }

        var releases = await writeRepository.GetByIdsAsync(releaseIds, cancellationToken);
        var normalizedLanguageCode = CleanOptional(primaryLanguageCode)?.ToLowerInvariant();

        foreach (var release in releases)
        {
            release.PrimaryLanguageCode = normalizedLanguageCode;
        }

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int releaseId, CancellationToken cancellationToken = default)
    {
        var release = await writeRepository.GetByIdAsync(releaseId, cancellationToken);
        writeRepository.Remove(release);

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkUploadsPostedAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var release = await writeRepository.GetByIdAsync(releaseId, cancellationToken);
        release.UploadsPostedAt = timeProvider.GetLocalNow();

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<UnmanagedConversionPreview> GetUnmanagedConversionPreviewAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var release = await writeRepository.GetByIdAsync(releaseId, cancellationToken);

        var canConvert =
            release.ReleaseType is ReleaseType.Managed
            && release.ArchiveConfigs.Count > 0
            && AllArchiveConfigsHaveCreatedArchive(release);

        var archivesInsideReleaseFolder = UnmanagedReleaseConverter.HasCreatedArchiveInside(
            release,
            release.ReleaseFolderPath
        );

        return new UnmanagedConversionPreview(
            ReleaseFolderPath: release.ReleaseFolderPath,
            CanConvert: canConvert,
            ArchivesInsideReleaseFolder: archivesInsideReleaseFolder
        );
    }

    public async Task ConvertToUnmanagedAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var release = await writeRepository.GetByIdAsync(releaseId, cancellationToken);

        if (release.ReleaseType is not ReleaseType.Managed)
        {
            throw new InvalidOperationException(
                "Only managed releases can be converted to unmanaged."
            );
        }

        if (release.ArchiveConfigs.Count == 0 || !AllArchiveConfigsHaveCreatedArchive(release))
        {
            throw new InvalidOperationException(
                "All archive configs must have a created archive before the release can be converted to unmanaged."
            );
        }

        UnmanagedReleaseConverter.ConvertToUnmanaged(release);

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task ConvertToManagedAsync(
        int releaseId,
        string releaseFolderPath,
        CancellationToken cancellationToken = default
    )
    {
        var release = await writeRepository.GetByIdAsync(releaseId, cancellationToken);

        if (release.ReleaseType is not ReleaseType.Unmanaged)
        {
            throw new InvalidOperationException(
                "Only unmanaged releases can be converted to managed."
            );
        }

        if (string.IsNullOrWhiteSpace(releaseFolderPath))
        {
            throw new InvalidOperationException(
                "A release folder must be assigned when converting to managed."
            );
        }

        release.ReleaseType = ReleaseType.Managed;
        release.ReleaseFolderPath = releaseFolderPath;

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<ArchiveDeletionPreview> GetArchiveDeletionPreviewAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var release = await writeRepository.GetForArchiveDeletionAsync(
            releaseId,
            cancellationToken
        );

        var mirrorUploads = mirrorCoverageEvaluator.GetMirrorUploadsPerArchiveConfig(release);
        var deletableArchives = GetDeletableArchives(release);

        var canDelete =
            deletableArchives.Count > 0 && !HasActiveUploads(release) && IsRecoverable(release);

        var deletableArchiveFolderPaths = deletableArchives
            .Select(archive => archive.ArchiveFolderPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        var mirrorHosterNames = mirrorUploads
            .Values.Select(upload => upload.UploadConfig.HosterRegistration.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        return new ArchiveDeletionPreview(
            CanDelete: canDelete,
            DeletableArchiveFolderPaths: deletableArchiveFolderPaths,
            MirrorHosterNames: mirrorHosterNames
        );
    }

    public async Task DeleteLocalArchivesAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var release = await writeRepository.GetForArchiveDeletionAsync(
            releaseId,
            cancellationToken
        );

        var deletableArchives = GetDeletableArchives(release);

        if (deletableArchives.Count == 0)
        {
            throw new InvalidOperationException(
                "This release has no local archives that could be deleted."
            );
        }

        if (HasActiveUploads(release))
        {
            throw new InvalidOperationException(
                "Local archives cannot be deleted while uploads of this release are still running."
            );
        }

        if (!IsRecoverable(release))
        {
            throw new InvalidOperationException(
                "Every archive config must have a fully online upload on a hoster that is enabled for mirror downloads, or the release must be a managed release with a release folder, before the local archives can be deleted."
            );
        }

        foreach (var archive in deletableArchives)
        {
            localArchiveDeleter.DeleteLocalArchive(archive);
        }

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    private static List<Archive> GetDeletableArchives(Release release)
    {
        return release
            .ArchiveConfigs.SelectMany(config => config.Archives)
            .Where(archive => archive.ArchiveState is ArchiveState.Created)
            .ToList();
    }

    private static bool HasActiveUploads(Release release)
    {
        return release
            .ArchiveConfigs.SelectMany(config => config.UploadConfigs)
            .SelectMany(uploadConfig => uploadConfig.Uploads)
            .Any(upload =>
                upload.UploadState
                    is UploadState.WaitingForArchive
                        or UploadState.Pending
                        or UploadState.Uploading
            );
    }

    private bool IsRecoverable(Release release)
    {
        return mirrorCoverageEvaluator.HasFullMirrorCoverage(release)
            || (
                release.ReleaseType is ReleaseType.Managed
                && !string.IsNullOrWhiteSpace(release.ReleaseFolderPath)
            );
    }

    private static bool AllArchiveConfigsHaveCreatedArchive(Release release)
    {
        return release.ArchiveConfigs.All(config =>
            config.Archives.Any(archive => archive.ArchiveState is ArchiveState.Created)
        );
    }

    public async Task<int> CreateFromTemplateAsync(
        int releaseTemplateId,
        string releaseFolderPath,
        string? name,
        string? primaryLanguageCode,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedFolderPath = Path.TrimEndingDirectorySeparator(releaseFolderPath.Trim());

        if (!fileSystemService.DirectoryExists(normalizedFolderPath))
        {
            throw new ReleaseFolderNotFoundException(normalizedFolderPath);
        }

        var releaseTemplate =
            await writeRepository.GetTemplateForReleaseCreationOrDefaultAsync(
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
            primaryLanguageCode: CleanOptional(primaryLanguageCode)?.ToLowerInvariant(),
            localNow: timeProvider.GetLocalNow(),
            cancellationToken: cancellationToken
        );

        writeRepository.Add(release);
        await writeRepository.SaveChangesAsync(cancellationToken);

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

    private static string? CleanOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
