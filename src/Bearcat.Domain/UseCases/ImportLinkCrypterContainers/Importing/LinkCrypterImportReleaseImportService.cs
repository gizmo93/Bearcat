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
        DateTime uploadsPostedAt,
        DateTime localNow,
        CancellationToken cancellationToken
    )
    {
        var archivePartsMove =
            existingRelease is null && releasePreview.ArchiveFolderContainsArchivesOfOtherReleases
                ? CreateArchivePartsMove(releasePreview)
                : null;

        var release =
            existingRelease
            ?? await CreateReleaseAsync(
                linkCrypterImport: linkCrypterImport,
                releasePreview: releasePreview,
                archivePartsMove: archivePartsMove,
                archiveStorageFolders: archiveStorageFolders,
                uploadsPostedAt: uploadsPostedAt,
                localNow: localNow,
                cancellationToken: cancellationToken
            );

        var archiveFolderPathWithOnlyArchiveOfRelease =
            releasePreview.ArchiveFolderContainsArchivesOfOtherReleases
                ? null
                : releasePreview.ArchiveFolderPath;

        var mirrorsWithUploadConfigs = GetMirrorsWithUploadConfigs(releasePreview, release);

        var sourceUploads = mirrorsWithUploadConfigs
            .Select(mirrorWithUploadConfig =>
                GetOrAddSourceUploadOrDefault(
                    linkCrypterImport: linkCrypterImport,
                    release: release,
                    mirror: mirrorWithUploadConfig.Mirror,
                    uploadConfig: mirrorWithUploadConfig.UploadConfig,
                    archiveFolderPath: archiveFolderPathWithOnlyArchiveOfRelease,
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
            SourceUploads: sourceUploads,
            ArchivePartsMove: archivePartsMove
        );
    }

    private ArchivePartsMove CreateArchivePartsMove(LinkCrypterImportReleasePreview releasePreview)
    {
        var sharedArchiveFolderPath = releasePreview.ArchiveFolderPath!;

        var sourceFilePaths = fileSystemService
            .GetFilesInPath(sharedArchiveFolderPath, recursive: false)
            .Where(filePath =>
                string.Equals(
                    ArchivePartFileNames.GetArchiveBaseName(Path.GetFileName(filePath)),
                    releasePreview.ReleaseName,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (sourceFilePaths.Count == 0)
        {
            throw new InvalidLinkCrypterImportException(
                $"Folder {sharedArchiveFolderPath} does not contain the archive parts of release {releasePreview.ReleaseName}."
            );
        }

        return new ArchivePartsMove(
            ReleaseName: releasePreview.ReleaseName,
            TargetFolderPath: Path.Combine(sharedArchiveFolderPath, releasePreview.ReleaseName),
            SourceFilePaths: sourceFilePaths
        );
    }

    private async Task<Release> CreateReleaseAsync(
        LinkCrypterImport linkCrypterImport,
        LinkCrypterImportReleasePreview releasePreview,
        ArchivePartsMove? archivePartsMove,
        IReadOnlyList<ArchiveStorageFolder> archiveStorageFolders,
        DateTime uploadsPostedAt,
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
                        archivePartsMove: archivePartsMove,
                        archiveStorageFolders: archiveStorageFolders,
                        localNow: localNow
                    ),
            primaryLanguageCode: linkCrypterImport.PrimaryLanguageCode,
            resolveReleaseInfo: false,
            localNow: localNow,
            cancellationToken: cancellationToken
        );

        releaseData.Release.UploadsPostedAt = uploadsPostedAt;
        releaseWriteRepository.Add(releaseData.Release);

        return releaseData.Release;
    }

    private ArchiveConfig CreateUnmanagedArchiveConfig(
        Release release,
        LinkCrypterImport linkCrypterImport,
        LinkCrypterImportReleasePreview releasePreview,
        ArchivePartsMove? archivePartsMove,
        IReadOnlyList<ArchiveStorageFolder> archiveStorageFolders,
        DateTime localNow
    )
    {
        if (releasePreview.ArchiveFolderPath is not null)
        {
            var archiveConfig = archivePartsMove is null
                ? unmanagedReleaseArchiveInitializationService.CreateArchiveConfig(
                    release: release,
                    archiveFolderPath: releasePreview.ArchiveFolderPath,
                    createdAt: localNow
                )
                : CreateArchiveConfigInTargetFolderOfArchivePartsMove(
                    release: release,
                    archivePartsMove: archivePartsMove,
                    localNow: localNow
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

    private ArchiveConfig CreateArchiveConfigInTargetFolderOfArchivePartsMove(
        Release release,
        ArchivePartsMove archivePartsMove,
        DateTime localNow
    )
    {
        var archiveFileNames = archivePartsMove
            .SourceFilePaths.Select(filePath => Path.GetFileName(filePath))
            .ToList();

        return unmanagedReleaseArchiveInitializationService.CreateArchiveConfigWithArchiveFiles(
            release: release,
            archiveFolderPath: archivePartsMove.TargetFolderPath,
            archiveFileNames: archiveFileNames,
            archiverFileExtension: ArchivePartFileNames.GetArchiverFileExtension(
                archiveFileNames[0]
            )!,
            createdAt: localNow
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
        var unusedUploadConfigs = GetArchiveFilesUploadConfigs(release);

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

    private static List<UploadConfig> GetArchiveFilesUploadConfigs(Release release)
    {
        return release
            .UploadConfigs.Where(uploadConfig =>
                uploadConfig.UploadSource is UploadSource.ArchiveFiles
            )
            .OrderBy(uploadConfig => uploadConfig.Id)
            .ToList();
    }

    private static Dictionary<string, ArchiveFile> GetArchiveFilesByFileName(Archive archive)
    {
        return archive.ArchiveFiles.ToDictionary(
            archiveFile => Path.GetFileName(archiveFile.FullFileName),
            StringComparer.OrdinalIgnoreCase
        );
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
            && HasFileLinks(upload, mirror.Files.Select(file => file.OwnFormatLink!).ToList());
    }

    private static bool HasFileLinks(Upload upload, List<string> hosterFileLinks)
    {
        return upload
            .UploadedFiles.Select(file => file.HosterFileLink)
            .ToHashSet(StringComparer.Ordinal)
            .SetEquals(hosterFileLinks);
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
        var archiveFilesByFileName = GetArchiveFilesByFileName(archive);

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
