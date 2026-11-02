using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.UnmanagedReleases;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.Creation;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Exceptions;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Repositories;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Importing;

public class LinkCrypterImportContainerImportService(
    ILinkCrypterImportRepository repository,
    IReleaseWriteRepository releaseWriteRepository,
    ReleaseFromFolderCreationService releaseFromFolderCreationService,
    UnmanagedReleaseArchiveInitializationService unmanagedReleaseArchiveInitializationService,
    INotificationService notificationService,
    TimeProvider timeProvider,
    ILogger<LinkCrypterImportContainerImportService> logger
)
{
    public async Task ImportSelectedContainersAsync(
        int linkCrypterImportId,
        CancellationToken cancellationToken
    )
    {
        var containerIds = await repository.GetIdsOfSelectedImportableContainersAsync(
            linkCrypterImportId,
            cancellationToken
        );

        foreach (var containerId in containerIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await ImportContainerOrMarkAsFailedAsync(containerId, cancellationToken);
        }

        await CompleteImportAsync(linkCrypterImportId, cancellationToken);
    }

    private async Task ImportContainerOrMarkAsFailedAsync(
        int containerId,
        CancellationToken cancellationToken
    )
    {
        repository.ClearChangeTracker();

        var container = await repository.GetContainerWithImportAsync(
            containerId,
            cancellationToken
        );

        try
        {
            await ImportContainerAsync(container, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Could not import container {ContainerUrl} of link crypter import {LinkCrypterImportId}",
                container.ContainerUrl,
                container.LinkCrypterImportId
            );

            repository.ClearChangeTracker();
            container = await repository.GetContainerWithImportAsync(
                containerId,
                cancellationToken
            );
            container.Status = LinkCrypterImportContainerStatus.ImportFailed;
            container.ErrorMessages = [exception.Message];
        }

        container.LinkCrypterImport.ProcessedContainerCount++;
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task ImportContainerAsync(
        LinkCrypterImportContainer container,
        CancellationToken cancellationToken
    )
    {
        var releaseName = container.ContainerName.Trim();
        var preview = LinkCrypterImportContainerPreview.FromJson(container.PreviewJson);
        var localNow = timeProvider.GetLocalNow();

        var existingRelease = await repository.GetReleaseForImportByNameOrDefaultAsync(
            releaseName,
            cancellationToken
        );

        var release =
            existingRelease
            ?? await CreateReleaseAsync(
                linkCrypterImport: container.LinkCrypterImport,
                releaseName: releaseName,
                preview: preview,
                localNow: localNow,
                cancellationToken: cancellationToken
            );

        var mirrorsWithUploadConfigs = GetMirrorsWithUploadConfigs(preview, release);

        EnsureContainerCanBeAdded(
            container: container,
            release: release,
            mirrorsWithUploadConfigs: mirrorsWithUploadConfigs
        );

        AddImportedUploadsAndLinkCrypterContainer(
            container: container,
            release: release,
            mirrorsWithUploadConfigs: mirrorsWithUploadConfigs,
            isNewRelease: existingRelease is null,
            localNow: localNow
        );

        container.ImportedRelease = release;
        container.Status = LinkCrypterImportContainerStatus.Imported;
        container.ErrorMessages = [];

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Imported container {ContainerUrl} into release {ReleaseName}",
            container.ContainerUrl,
            releaseName
        );
    }

    private async Task<Release> CreateReleaseAsync(
        LinkCrypterImport linkCrypterImport,
        string releaseName,
        LinkCrypterImportContainerPreview preview,
        DateTime localNow,
        CancellationToken cancellationToken
    )
    {
        var isManaged = preview.ReleaseFolderPath is not null;
        var hasAdoptedMirror = preview.Mirrors.Any(mirror =>
            mirror.Decision is LinkCrypterImportMirrorDecision.Adopt
        );

        if (
            !isManaged
            && (
                !hasAdoptedMirror
                || linkCrypterImport.ArchiveFilesBasePathForReleasesWithoutLocalFiles is null
            )
        )
        {
            throw new InvalidLinkCrypterImportException(
                $"Release {releaseName} cannot be created: there is no local release folder and no adopted mirror with an archive base folder."
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
            releaseFolderPath: preview.ReleaseFolderPath,
            createUnmanagedArchiveConfig: isManaged
                ? null
                : release =>
                    CreateArchiveConfigWithDeletedArchive(
                        release: release,
                        archiveFilesBasePath: linkCrypterImport.ArchiveFilesBasePathForReleasesWithoutLocalFiles!,
                        preview: preview,
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

    private ArchiveConfig CreateArchiveConfigWithDeletedArchive(
        Release release,
        string archiveFilesBasePath,
        LinkCrypterImportContainerPreview preview,
        DateTime localNow
    )
    {
        var archiveFileNames = GetFileNames(
            preview.Mirrors.First(mirror =>
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

    private static List<(
        LinkCrypterImportMirror Mirror,
        UploadConfig UploadConfig
    )> GetMirrorsWithUploadConfigs(LinkCrypterImportContainerPreview preview, Release release)
    {
        var unusedUploadConfigs = release
            .UploadConfigs.Where(uploadConfig =>
                uploadConfig.UploadSource is UploadSource.ArchiveFiles
            )
            .OrderBy(uploadConfig => uploadConfig.Id)
            .ToList();

        var mirrorsWithUploadConfigs = new List<(LinkCrypterImportMirror, UploadConfig)>();

        foreach (
            var mirror in preview
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
            mirrorsWithUploadConfigs.Add((mirror, uploadConfig));
        }

        return mirrorsWithUploadConfigs;
    }

    private static void EnsureContainerCanBeAdded(
        LinkCrypterImportContainer container,
        Release release,
        List<(LinkCrypterImportMirror Mirror, UploadConfig UploadConfig)> mirrorsWithUploadConfigs
    )
    {
        if (mirrorsWithUploadConfigs.Count == 0)
        {
            throw new InvalidLinkCrypterImportException(
                $"No mirror of the container belongs to a hoster of release {release.Name}."
            );
        }

        var linkCrypterRegistrationId = container.LinkCrypterImport.LinkCrypterRegistrationId;
        var linkCryptersOfRegistration = mirrorsWithUploadConfigs
            .SelectMany(mirrorWithUploadConfig => mirrorWithUploadConfig.UploadConfig.LinkCrypters)
            .Where(linkCrypter =>
                linkCrypter.LinkCrypterRegistrationId == linkCrypterRegistrationId
            )
            .ToList();

        var hasAllHostersContainer = release.LinkCrypterContainers.Any(linkCrypterContainer =>
            linkCrypterContainer.Scope is LinkCrypterContainerScope.ReleaseAllHosters
            && linkCrypterContainer.LinkCrypterRegistrationId == linkCrypterRegistrationId
        );

        var isPartOfAllHostersContainer =
            mirrorsWithUploadConfigs.Count > 1
            || linkCryptersOfRegistration.Any(linkCrypter =>
                linkCrypter.ContainerScope is LinkCrypterContainerScope.ReleaseAllHosters
            );

        if (
            linkCryptersOfRegistration.Any(linkCrypter =>
                linkCrypter.LinkCrypterContainers.Count > 0
            ) || (hasAllHostersContainer && isPartOfAllHostersContainer)
        )
        {
            throw new InvalidLinkCrypterImportException(
                $"Release {release.Name} already has a container of this link crypter account for a hoster of the container."
            );
        }
    }

    private static void AddImportedUploadsAndLinkCrypterContainer(
        LinkCrypterImportContainer container,
        Release release,
        List<(LinkCrypterImportMirror Mirror, UploadConfig UploadConfig)> mirrorsWithUploadConfigs,
        bool isNewRelease,
        DateTime localNow
    )
    {
        var linkCrypterImport = container.LinkCrypterImport;

        var containerScope =
            mirrorsWithUploadConfigs.Count == 1
                ? LinkCrypterContainerScope.Release
                : LinkCrypterContainerScope.ReleaseAllHosters;

        var sourceUploads = mirrorsWithUploadConfigs
            .Select(mirrorWithUploadConfig =>
                GetOrAddSourceUploadOrDefault(
                    linkCrypterImport: linkCrypterImport,
                    mirror: mirrorWithUploadConfig.Mirror,
                    uploadConfig: mirrorWithUploadConfig.UploadConfig,
                    localNow: localNow
                )
            )
            .OfType<Upload>()
            .ToList();

        var linkCrypters = mirrorsWithUploadConfigs
            .Select(mirrorWithUploadConfig =>
                GetOrAddLinkCrypter(
                    uploadConfig: mirrorWithUploadConfig.UploadConfig,
                    linkCrypterRegistrationId: linkCrypterImport.LinkCrypterRegistrationId,
                    containerScope: containerScope
                )
            )
            .ToList();

        if (isNewRelease && containerScope is LinkCrypterContainerScope.ReleaseAllHosters)
        {
            foreach (
                var uploadConfig in release.UploadConfigs.Where(uploadConfig =>
                    uploadConfig.UploadSource is UploadSource.ArchiveFiles
                )
            )
            {
                GetOrAddLinkCrypter(
                    uploadConfig: uploadConfig,
                    linkCrypterRegistrationId: linkCrypterImport.LinkCrypterRegistrationId,
                    containerScope: containerScope
                );
            }
        }

        AddLinkCrypterContainer(
            container: container,
            release: release,
            containerScope: containerScope,
            linkCrypters: linkCrypters,
            sourceUploads: sourceUploads,
            localNow: localNow
        );
    }

    private static Upload? GetOrAddSourceUploadOrDefault(
        LinkCrypterImport linkCrypterImport,
        LinkCrypterImportMirror mirror,
        UploadConfig uploadConfig,
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

    private static UploadConfigLinkCrypter GetOrAddLinkCrypter(
        UploadConfig uploadConfig,
        int linkCrypterRegistrationId,
        LinkCrypterContainerScope containerScope
    )
    {
        var linkCrypter = uploadConfig.LinkCrypters.FirstOrDefault(linkCrypter =>
            linkCrypter.LinkCrypterRegistrationId == linkCrypterRegistrationId
        );

        if (linkCrypter is null)
        {
            linkCrypter = new UploadConfigLinkCrypter
            {
                LinkCrypterRegistrationId = linkCrypterRegistrationId,
                LinkCrypterContainers = [],
            };
            uploadConfig.LinkCrypters.Add(linkCrypter);
        }

        linkCrypter.ContainerScope = containerScope;

        return linkCrypter;
    }

    private static void AddLinkCrypterContainer(
        LinkCrypterImportContainer container,
        Release release,
        LinkCrypterContainerScope containerScope,
        List<UploadConfigLinkCrypter> linkCrypters,
        List<Upload> sourceUploads,
        DateTime localNow
    )
    {
        var linkCrypterContainer = new LinkCrypterContainer
        {
            Scope = containerScope,
            LinkCrypterRegistrationId = container.LinkCrypterImport.LinkCrypterRegistrationId,
            ContainerUrl = container.ContainerUrl,
            ExternalReference = container.ExternalReference,
            State = LinkCrypterContainerState.Created,
            Errors = [],
            CreatedAt = localNow,
            SourceUploads = [],
            Notifications = [],
        };
        linkCrypterContainer.CopySettingsFrom(linkCrypters[0]);

        if (containerScope is LinkCrypterContainerScope.ReleaseAllHosters)
        {
            linkCrypterContainer.Release = release;
            linkCrypterContainer.SourceUploads = sourceUploads
                .Select(upload => new LinkCrypterContainerSourceUpload
                {
                    LinkCrypterContainer = linkCrypterContainer,
                    Upload = upload,
                })
                .ToList();
            release.LinkCrypterContainers.Add(linkCrypterContainer);
            return;
        }

        linkCrypterContainer.UploadConfigLinkCrypter = linkCrypters[0];
        linkCrypterContainer.Upload = sourceUploads.SingleOrDefault();
        linkCrypters[0].LinkCrypterContainers.Add(linkCrypterContainer);
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

    private static Archive? GetOrAddArchiveWithFileNamesOrDefault(
        ArchiveConfig archiveConfig,
        List<string> fileNames,
        DateTime localNow
    )
    {
        var latestArchive = archiveConfig.Archives.MaxBy(archive => archive.Id);

        if (latestArchive is not null)
        {
            return HasFileNames(latestArchive, fileNames) ? latestArchive : null;
        }

        var archive = UnmanagedReleaseArchiveInitializationService.BuildDeletedArchive(
            archiveFolderPath: archiveConfig.ArchiveFilesBasePath,
            archiveFileNames: fileNames,
            archiveFileSizeMb: archiveConfig.ArchiveFileSizeMb,
            createdAt: localNow
        );
        archiveConfig.Archives.Add(archive);

        return archive;
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

    private async Task CompleteImportAsync(
        int linkCrypterImportId,
        CancellationToken cancellationToken
    )
    {
        repository.ClearChangeTracker();

        var linkCrypterImport = await repository.GetWithContainersAsync(
            linkCrypterImportId,
            cancellationToken
        );

        var importedContainerCount = linkCrypterImport.Containers.Count(container =>
            container.Status is LinkCrypterImportContainerStatus.Imported
        );

        var failedContainerCount = linkCrypterImport.Containers.Count(container =>
            container.Status is LinkCrypterImportContainerStatus.ImportFailed
        );

        linkCrypterImport.ChangeState(LinkCrypterImportState.Completed, timeProvider.GetLocalNow());
        notificationService.Create(
            kind: failedContainerCount == 0
                ? NotificationKind.LinkCrypterImportCompleted
                : NotificationKind.LinkCrypterImportFailed,
            message: $"Link crypter import {linkCrypterImportId} finished: {importedContainerCount} containers were imported, {failedContainerCount} containers failed."
        );
        await repository.SaveChangesAsync(cancellationToken);
    }
}
