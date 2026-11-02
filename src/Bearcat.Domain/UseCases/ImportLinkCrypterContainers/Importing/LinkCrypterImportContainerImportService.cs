using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.UnmanagedReleases;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.Creation;
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

        if (await repository.ReleaseWithNameExistsAsync(releaseName, cancellationToken))
        {
            container.Status = LinkCrypterImportContainerStatus.ImportFailed;
            container.ErrorMessages = [$"A release named {releaseName} already exists."];
            return;
        }

        var preview = LinkCrypterImportContainerPreview.FromJson(container.PreviewJson);
        var localNow = timeProvider.GetLocalNow();
        var releaseData = await CreateReleaseAsync(
            linkCrypterImport: container.LinkCrypterImport,
            releaseName: releaseName,
            preview: preview,
            localNow: localNow,
            cancellationToken: cancellationToken
        );

        AddImportedUploadsAndLinkCrypterContainer(
            container: container,
            releaseData: releaseData,
            preview: preview,
            localNow: localNow
        );

        container.ImportedRelease = releaseData.Release;
        container.Status = LinkCrypterImportContainerStatus.Imported;
        container.ErrorMessages = [];

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Imported container {ContainerUrl} into release {ReleaseName}",
            container.ContainerUrl,
            releaseName
        );
    }

    private async Task<ReleaseFromTemplateData> CreateReleaseAsync(
        LinkCrypterImport linkCrypterImport,
        string releaseName,
        LinkCrypterImportContainerPreview preview,
        DateTime localNow,
        CancellationToken cancellationToken
    )
    {
        var releaseTemplate =
            await releaseWriteRepository.GetTemplateForReleaseCreationOrDefaultAsync(
                linkCrypterImport.ReleaseTemplateId,
                cancellationToken
            )
            ?? throw new InvalidOperationException(
                $"Release template {linkCrypterImport.ReleaseTemplateId} was not found."
            );

        var isManaged = preview.ReleaseFolderPath is not null;

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
            primaryLanguageCode: null,
            localNow: localNow,
            cancellationToken: cancellationToken
        );

        releaseData.Release.UploadsPostedAt = localNow;
        releaseWriteRepository.Add(releaseData.Release);

        return releaseData;
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

    private static void AddImportedUploadsAndLinkCrypterContainer(
        LinkCrypterImportContainer container,
        ReleaseFromTemplateData releaseData,
        LinkCrypterImportContainerPreview preview,
        DateTime localNow
    )
    {
        var linkCrypterImport = container.LinkCrypterImport;

        var uploadConfigsByTemplateId = releaseData.UploadConfigMatches.ToDictionary(
            match => match.UploadConfigTemplate.Id,
            match => match.UploadConfig
        );

        var containerMirrors = preview
            .Mirrors.Where(mirror =>
                mirror.Decision
                    is LinkCrypterImportMirrorDecision.Adopt
                        or LinkCrypterImportMirrorDecision.UploadByBearcat
            )
            .ToList();

        var containerScope =
            containerMirrors.Count == 1
                ? LinkCrypterContainerScope.Release
                : LinkCrypterContainerScope.ReleaseAllHosters;

        var adoptedUploads = containerMirrors
            .Where(mirror => mirror.Decision is LinkCrypterImportMirrorDecision.Adopt)
            .Select(mirror =>
                AddImportedUpload(
                    uploadConfig: uploadConfigsByTemplateId[mirror.UploadConfigTemplateId!.Value],
                    mirror: mirror,
                    hosterRegistrationId: GetChosenHosterRegistrationId(linkCrypterImport, mirror),
                    localNow: localNow
                )
            )
            .ToList();

        var linkCrypters = containerMirrors
            .Select(mirror =>
                GetOrAddLinkCrypter(
                    uploadConfig: uploadConfigsByTemplateId[mirror.UploadConfigTemplateId!.Value],
                    linkCrypterRegistrationId: linkCrypterImport.LinkCrypterRegistrationId,
                    containerScope: containerScope
                )
            )
            .ToList();

        AddLinkCrypterContainer(
            container: container,
            release: releaseData.Release,
            containerScope: containerScope,
            linkCrypters: linkCrypters,
            adoptedUploads: adoptedUploads,
            localNow: localNow
        );
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
        List<Upload> adoptedUploads,
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
            linkCrypterContainer.SourceUploads = adoptedUploads
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
        linkCrypterContainer.Upload = adoptedUploads.SingleOrDefault();
        linkCrypters[0].LinkCrypterContainers.Add(linkCrypterContainer);
    }

    private static Upload AddImportedUpload(
        UploadConfig uploadConfig,
        LinkCrypterImportMirror mirror,
        int hosterRegistrationId,
        DateTime localNow
    )
    {
        var archive = GetOrAddArchive(uploadConfig.ArchiveConfig!, GetFileNames(mirror), localNow);
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
            OnlineState = OnlineState.Unknown,
            PremiumOnlyDownload = uploadConfig.PremiumOnlyDownload,
            ErrorMessages = [],
            LinkCrypterContainers = [],
            Notifications = [],
            UploadedFiles = mirror
                .Files.Select(file => new UploadedFile
                {
                    HosterFileLink = file.OwnFormatLink!,
                    ArchiveFile = archiveFilesByFileName[file.FileName!],
                    OnlineState = OnlineState.Unknown,
                    ErrorMessages = [],
                    CreatedAt = localNow,
                })
                .ToList(),
        };

        uploadConfig.Uploads.Add(upload);

        return upload;
    }

    private static Archive GetOrAddArchive(
        ArchiveConfig archiveConfig,
        List<string> fileNames,
        DateTime localNow
    )
    {
        var existingArchive = archiveConfig.Archives.FirstOrDefault();

        if (existingArchive is not null)
        {
            return existingArchive;
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
