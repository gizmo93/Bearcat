using Bearcat.Abstractions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.UnmanagedReleases;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.Creation;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Exceptions;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Importing;

public class LinkCrypterImportReleaseImportService(
    IReleaseWriteRepository releaseWriteRepository,
    ReleaseFromFolderCreationService releaseFromFolderCreationService,
    UnmanagedReleaseArchiveInitializationService unmanagedReleaseArchiveInitializationService,
    IFileSystemService fileSystemService
)
{
    public async Task<LinkCrypterImportedRelease> ImportReleaseAsync(
        LinkCrypterImport linkCrypterImport,
        LinkCrypterImportReleasePreview releasePreview,
        Release? existingRelease,
        IReadOnlyList<ArchiveStorageFolder> archiveStorageFolders,
        DateTime localNow,
        CancellationToken cancellationToken
    )
    {
        var release =
            existingRelease
            ?? await CreateReleaseAsync(
                linkCrypterImport: linkCrypterImport,
                releasePreview: releasePreview,
                archiveStorageFolders: archiveStorageFolders,
                localNow: localNow,
                cancellationToken: cancellationToken
            );

        var mirrorsWithUploadConfigs = GetMirrorsWithUploadConfigs(releasePreview, release);

        var sourceUploads = mirrorsWithUploadConfigs
            .Select(mirrorWithUploadConfig =>
                GetOrAddSourceUploadOrDefault(
                    linkCrypterImport: linkCrypterImport,
                    release: release,
                    mirror: mirrorWithUploadConfig.Mirror,
                    uploadConfig: mirrorWithUploadConfig.UploadConfig,
                    archiveFolderPath: releasePreview.ArchiveFolderPath,
                    archiveStorageFolders: archiveStorageFolders,
                    localNow: localNow
                )
            )
            .OfType<Upload>()
            .ToList();

        return new LinkCrypterImportedRelease(
            Release: release,
            IsNewRelease: existingRelease is null,
            MirrorsWithUploadConfigs: mirrorsWithUploadConfigs,
            SourceUploads: sourceUploads
        );
    }

    private async Task<Release> CreateReleaseAsync(
        LinkCrypterImport linkCrypterImport,
        LinkCrypterImportReleasePreview releasePreview,
        IReadOnlyList<ArchiveStorageFolder> archiveStorageFolders,
        DateTime localNow,
        CancellationToken cancellationToken
    )
    {
        var releaseName = releasePreview.ReleaseName;
        var isManaged = releasePreview.ReleaseFolderPath is not null;
        var hasAdoptedMirror = releasePreview.Mirrors.Any(mirror =>
            mirror.Decision is LinkCrypterImportMirrorDecision.Adopt
        );

        if (
            !isManaged
            && releasePreview.ArchiveFolderPath is null
            && (
                !hasAdoptedMirror
                || linkCrypterImport.ArchiveFilesBasePathForReleasesWithoutLocalFiles is null
            )
        )
        {
            throw new InvalidLinkCrypterImportException(
                $"Release {releaseName} cannot be created: there is no local release folder, no existing archive folder and no adopted mirror with an archive base folder."
            );
        }

        var releaseTemplate =
            await releaseWriteRepository.GetTemplateForReleaseCreationOrDefaultAsync(
                linkCrypterImport.ReleaseTemplateId,
                cancellationToken
            )
            ?? throw new InvalidOperationException(
                $"Release template {linkCrypterImport.ReleaseTemplateId} was not found."
            );

        var releaseData = await releaseFromFolderCreationService.CreateWithReleaseTypeAsync(
            releaseTemplate: releaseTemplate,
            releaseType: isManaged ? ReleaseType.Managed : ReleaseType.Unmanaged,
            releaseName: releaseName,
            releaseFolderPath: releasePreview.ReleaseFolderPath,
            createUnmanagedArchiveConfig: isManaged
                ? null
                : release =>
                    CreateUnmanagedArchiveConfig(
                        release: release,
                        linkCrypterImport: linkCrypterImport,
                        releasePreview: releasePreview,
                        archiveStorageFolders: archiveStorageFolders,
                        localNow: localNow
                    ),
            primaryLanguageCode: linkCrypterImport.PrimaryLanguageCode,
            resolveReleaseInfo: false,
            localNow: localNow,
            cancellationToken: cancellationToken
        );

        releaseData.Release.UploadsPostedAt = localNow;
        releaseWriteRepository.Add(releaseData.Release);

        return releaseData.Release;
    }

    private ArchiveConfig CreateUnmanagedArchiveConfig(
        Release release,
        LinkCrypterImport linkCrypterImport,
        LinkCrypterImportReleasePreview releasePreview,
        IReadOnlyList<ArchiveStorageFolder> archiveStorageFolders,
        DateTime localNow
    )
    {
        if (releasePreview.ArchiveFolderPath is not null)
        {
            var archiveConfig = unmanagedReleaseArchiveInitializationService.CreateArchiveConfig(
                release: release,
                archiveFolderPath: releasePreview.ArchiveFolderPath,
                createdAt: localNow
            );

            UnmanagedReleaseArchiveInitializationService.AssignArchiveStorageFolderContainingArchiveFolder(
                archiveConfig.Archives.Single(),
                archiveStorageFolders
            );

            return archiveConfig;
        }

        return CreateArchiveConfigWithDeletedArchive(
            release: release,
            archiveFilesBasePath: linkCrypterImport.ArchiveFilesBasePathForReleasesWithoutLocalFiles!,
            releasePreview: releasePreview,
            localNow: localNow
        );
    }

    private ArchiveConfig CreateArchiveConfigWithDeletedArchive(
        Release release,
        string archiveFilesBasePath,
        LinkCrypterImportReleasePreview releasePreview,
        DateTime localNow
    )
    {
        var archiveFileNames = GetFileNames(
            releasePreview.Mirrors.First(mirror =>
                mirror.Decision is LinkCrypterImportMirrorDecision.Adopt
            )
        );

        return unmanagedReleaseArchiveInitializationService.CreateArchiveConfigWithDeletedArchive(
            release: release,
            archiveFilesBasePath: archiveFilesBasePath,
            archiveFileNames: archiveFileNames,
            archiverFileExtension: ArchivePartFileNames.GetArchiverFileExtension(
                archiveFileNames[0]
            )!,
            createdAt: localNow
        );
    }

    private static List<LinkCrypterImportMirrorWithUploadConfig> GetMirrorsWithUploadConfigs(
        LinkCrypterImportReleasePreview releasePreview,
        Release release
    )
    {
        var unusedUploadConfigs = release
            .UploadConfigs.Where(uploadConfig =>
                uploadConfig.UploadSource is UploadSource.ArchiveFiles
            )
            .OrderBy(uploadConfig => uploadConfig.Id)
            .ToList();

        var mirrorsWithUploadConfigs = new List<LinkCrypterImportMirrorWithUploadConfig>();

        foreach (
            var mirror in releasePreview
                .Mirrors.Where(mirror => mirror.HosterClassName is not null)
                .OrderBy(mirror => mirror.MirrorNumber)
        )
        {
            var uploadConfig = unusedUploadConfigs.FirstOrDefault(uploadConfig =>
                uploadConfig.GetHosterClassName() == mirror.HosterClassName
            );

            if (uploadConfig is null)
            {
                continue;
            }

            unusedUploadConfigs.Remove(uploadConfig);

            mirrorsWithUploadConfigs.Add(
                new LinkCrypterImportMirrorWithUploadConfig(mirror, uploadConfig)
            );
        }

        return mirrorsWithUploadConfigs;
    }

    private Upload? GetOrAddSourceUploadOrDefault(
        LinkCrypterImport linkCrypterImport,
        Release release,
        LinkCrypterImportMirror mirror,
        UploadConfig uploadConfig,
        string? archiveFolderPath,
        IReadOnlyList<ArchiveStorageFolder> archiveStorageFolders,
        DateTime localNow
    )
    {
        var completedUploads = uploadConfig
            .Uploads.Where(upload => upload.UploadState is UploadState.Completed)
            .ToList();

        var uploadWithSameLinks = completedUploads.FirstOrDefault(upload =>
            HasSameLinks(upload, mirror)
        );

        if (uploadWithSameLinks is not null)
        {
            return uploadWithSameLinks;
        }

        if (
            completedUploads.Count > 0
            || mirror.Decision is not LinkCrypterImportMirrorDecision.Adopt
        )
        {
            return null;
        }

        var archive = GetOrAddArchiveWithFileNamesOrDefault(
            archiveConfig: uploadConfig.ArchiveConfig!,
            fileNames: GetFileNames(mirror),
            existingArchiveFolderPath: IsArchiveFolderUsedByArchiveOfRelease(
                release,
                archiveFolderPath
            )
                ? null
                : archiveFolderPath,
            archiveStorageFolders: archiveStorageFolders,
            localNow: localNow
        );

        if (archive is null)
        {
            return null;
        }

        return AddImportedUpload(
            uploadConfig: uploadConfig,
            archive: archive,
            mirror: mirror,
            hosterRegistrationId: GetChosenHosterRegistrationId(linkCrypterImport, mirror),
            localNow: localNow
        );
    }

    private static bool HasSameLinks(Upload upload, LinkCrypterImportMirror mirror)
    {
        return mirror.Files.All(file => file.OwnFormatLink is not null)
            && upload
                .UploadedFiles.Select(file => file.HosterFileLink)
                .ToHashSet(StringComparer.Ordinal)
                .SetEquals(mirror.Files.Select(file => file.OwnFormatLink!));
    }

    private static int GetChosenHosterRegistrationId(
        LinkCrypterImport linkCrypterImport,
        LinkCrypterImportMirror mirror
    )
    {
        return linkCrypterImport
            .HosterRegistrations.Single(choice => choice.HosterClassName == mirror.HosterClassName)
            .HosterRegistrationId!.Value;
    }

    private static Upload AddImportedUpload(
        UploadConfig uploadConfig,
        Archive archive,
        LinkCrypterImportMirror mirror,
        int hosterRegistrationId,
        DateTime localNow
    )
    {
        var archiveFilesByFileName = archive.ArchiveFiles.ToDictionary(
            archiveFile => Path.GetFileName(archiveFile.FullFileName),
            StringComparer.OrdinalIgnoreCase
        );

        var upload = new Upload
        {
            UploadConfig = uploadConfig,
            HosterRegistrationId = hosterRegistrationId,
            Archive = archive,
            CreatedAt = localNow,
            UploadedAt = localNow,
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Online,
            PremiumOnlyDownload = uploadConfig.PremiumOnlyDownload,
            ErrorMessages = [],
            LinkCrypterContainers = [],
            Notifications = [],
            UploadedFiles = mirror
                .Files.Select(file => new UploadedFile
                {
                    HosterFileLink = file.OwnFormatLink!,
                    ArchiveFile = archiveFilesByFileName[file.FileName!],
                    OnlineState = OnlineState.Online,
                    ErrorMessages = [],
                    CreatedAt = localNow,
                })
                .ToList(),
        };

        uploadConfig.Uploads.Add(upload);

        return upload;
    }

    private static bool IsArchiveFolderUsedByArchiveOfRelease(
        Release release,
        string? archiveFolderPath
    )
    {
        return release
            .UploadConfigs.Select(uploadConfig => uploadConfig.ArchiveConfig)
            .OfType<ArchiveConfig>()
            .SelectMany(archiveConfig => archiveConfig.Archives)
            .Any(archive => archive.ArchiveFolderPath == archiveFolderPath);
    }

    private Archive? GetOrAddArchiveWithFileNamesOrDefault(
        ArchiveConfig archiveConfig,
        List<string> fileNames,
        string? existingArchiveFolderPath,
        IReadOnlyList<ArchiveStorageFolder> archiveStorageFolders,
        DateTime localNow
    )
    {
        var latestArchive = archiveConfig.Archives.MaxBy(archive => archive.Id);

        if (latestArchive is not null)
        {
            return HasFileNames(latestArchive, fileNames) ? latestArchive : null;
        }

        var archive = existingArchiveFolderPath is null
            ? UnmanagedReleaseArchiveInitializationService.BuildDeletedArchive(
                archiveFolderPath: archiveConfig.ArchiveFilesBasePath,
                archiveFileNames: fileNames,
                archiveFileSizeMb: archiveConfig.ArchiveFileSizeMb,
                createdAt: localNow
            )
            : BuildArchiveInExistingArchiveFolder(
                existingArchiveFolderPath: existingArchiveFolderPath,
                fileNames: fileNames,
                archiveStorageFolders: archiveStorageFolders,
                localNow: localNow
            );

        archiveConfig.Archives.Add(archive);

        return archive;
    }

    private Archive BuildArchiveInExistingArchiveFolder(
        string existingArchiveFolderPath,
        List<string> fileNames,
        IReadOnlyList<ArchiveStorageFolder> archiveStorageFolders,
        DateTime localNow
    )
    {
        var archive = UnmanagedReleaseArchiveInitializationService.BuildArchive(
            archiveFolderPath: existingArchiveFolderPath,
            archiveFilePaths: GetFilePathsWithFileNames(existingArchiveFolderPath, fileNames),
            createdAt: localNow
        );

        UnmanagedReleaseArchiveInitializationService.AssignArchiveStorageFolderContainingArchiveFolder(
            archive,
            archiveStorageFolders
        );

        return archive;
    }

    private List<string> GetFilePathsWithFileNames(string folderPath, List<string> fileNames)
    {
        return fileSystemService
            .GetFilesInPath(folderPath, recursive: false)
            .Where(filePath =>
                fileNames.Contains(Path.GetFileName(filePath), StringComparer.OrdinalIgnoreCase)
            )
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool HasFileNames(Archive archive, List<string> fileNames)
    {
        return archive
            .ArchiveFiles.Select(archiveFile => Path.GetFileName(archiveFile.FullFileName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            .SetEquals(fileNames);
    }

    private static List<string> GetFileNames(LinkCrypterImportMirror mirror)
    {
        return mirror.Files.Select(file => file.FileName!).ToList();
    }
}
