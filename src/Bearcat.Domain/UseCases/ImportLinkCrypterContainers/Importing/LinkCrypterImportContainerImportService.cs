using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.CollectionAssignment;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Exceptions;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Importing;

public class LinkCrypterImportContainerImportService(
    ILinkCrypterImportRepository repository,
    LinkCrypterImportReleaseImportService releaseImportService,
    CollectionContainerImportService collectionContainerImportService,
    ArchivePartMover archivePartMover,
    IReleaseCollectionAssigner releaseCollectionAssigner,
    INotificationService notificationService,
    TimeProvider timeProvider,
    ILogger<LinkCrypterImportContainerImportService> logger
)
{
    private const string ContainerNotAdoptedBecauseOfReleaseCollectionMessage =
        "The container was not adopted because the release belongs to a release collection whose upload slot uses this link crypter account. Bearcat adds the release to the collection container.";

    public async Task ImportSelectedContainersAsync(
        int linkCrypterImportId,
        CancellationToken cancellationToken
    )
    {
        var containerIds = await repository.GetIdsOfSelectedImportableContainersAsync(
            linkCrypterImportId,
            cancellationToken
        );

        var releaseCountWithMovedArchiveParts = 0;

        foreach (var containerId in containerIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            releaseCountWithMovedArchiveParts += await ImportContainerOrMarkAsFailedAsync(
                containerId,
                cancellationToken
            );
        }

        await CompleteImportAsync(
            linkCrypterImportId: linkCrypterImportId,
            releaseCountWithMovedArchiveParts: releaseCountWithMovedArchiveParts,
            cancellationToken: cancellationToken
        );
    }

    private async Task<int> ImportContainerOrMarkAsFailedAsync(
        int containerId,
        CancellationToken cancellationToken
    )
    {
        repository.ClearChangeTracker();
        releaseCollectionAssigner.ClearCachedReleaseCollections();

        var container = await repository.GetContainerWithImportAsync(
            containerId,
            cancellationToken
        );

        var releaseCountWithMovedArchiveParts = 0;

        try
        {
            releaseCountWithMovedArchiveParts = await ImportContainerAsync(
                container,
                cancellationToken
            );
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

        return releaseCountWithMovedArchiveParts;
    }

    private async Task<int> ImportContainerAsync(
        LinkCrypterImportContainer container,
        CancellationToken cancellationToken
    )
    {
        var preview = LinkCrypterImportContainerPreview.FromJson(container.PreviewJson);

        if (preview.Releases!.Count > 1)
        {
            var importResult = await collectionContainerImportService.ImportAsync(
                container: container,
                releasePreviews: preview.Releases,
                uploadsToMove: [],
                uploadsPostedAt: null,
                cancellationToken: cancellationToken
            );

            archivePartMover.MoveArchiveParts(importResult.ArchivePartsMoves);
            await repository.SaveChangesAsync(cancellationToken);

            return importResult.ArchivePartsMoves.Count;
        }

        await ImportSingleReleaseContainerAsync(
            container: container,
            releasePreview: preview.Releases.Single(),
            cancellationToken: cancellationToken
        );

        await repository.SaveChangesAsync(cancellationToken);

        return 0;
    }

    private async Task ImportSingleReleaseContainerAsync(
        LinkCrypterImportContainer container,
        LinkCrypterImportReleasePreview releasePreview,
        CancellationToken cancellationToken
    )
    {
        var localNow = timeProvider.GetLocalNow();

        var existingRelease = await repository.GetReleaseForImportByNameOrDefaultAsync(
            container.ReleaseName,
            cancellationToken
        );

        var archiveStorageFolders = await repository.GetArchiveStorageFoldersAsync(
            cancellationToken
        );

        var importedRelease = await releaseImportService.ImportReleaseAsync(
            linkCrypterImport: container.LinkCrypterImport,
            releasePreview: releasePreview,
            existingRelease: existingRelease,
            uploadsToMove: [],
            archiveStorageFolders: archiveStorageFolders,
            uploadsPostedAt: localNow,
            localNow: localNow,
            cancellationToken: cancellationToken
        );

        EnsureContainerCanBeAdded(
            container: container,
            release: importedRelease.Release,
            mirrorsWithUploadConfigs: importedRelease.MirrorsWithUploadConfigs
        );

        var uploadConfigsWithLinkCrypter = GetUploadConfigsReceivingLinkCrypter(importedRelease);
        var linkCrypterRegistrationId = container.LinkCrypterImport.LinkCrypterRegistrationId;

        if (
            uploadConfigsWithLinkCrypter.Any(uploadConfig =>
                HasReleaseCollectionLinkCrypter(uploadConfig, linkCrypterRegistrationId)
            )
        )
        {
            container.ErrorMessages = [ContainerNotAdoptedBecauseOfReleaseCollectionMessage];

            logger.LogInformation(
                "Imported the uploads of container {ContainerUrl} into release {ReleaseName} without adopting the container because the release collection uses this link crypter account",
                container.ContainerUrl,
                container.ReleaseName
            );
        }
        else
        {
            AddLinkCryptersAndLinkCrypterContainer(
                container: container,
                importedRelease: importedRelease,
                uploadConfigsWithLinkCrypter: uploadConfigsWithLinkCrypter,
                localNow: localNow
            );

            container.ErrorMessages = [];

            logger.LogInformation(
                "Imported container {ContainerUrl} into release {ReleaseName}",
                container.ContainerUrl,
                container.ReleaseName
            );
        }

        container.ImportedRelease = importedRelease.Release;
        container.Status = LinkCrypterImportContainerStatus.Imported;
    }

    private static void EnsureContainerCanBeAdded(
        LinkCrypterImportContainer container,
        Release release,
        IReadOnlyList<LinkCrypterImportMirrorWithUploadConfig> mirrorsWithUploadConfigs
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

    private static List<UploadConfig> GetUploadConfigsReceivingLinkCrypter(
        LinkCrypterImportedRelease importedRelease
    )
    {
        var uploadConfigs = importedRelease
            .MirrorsWithUploadConfigs.Select(mirrorWithUploadConfig =>
                mirrorWithUploadConfig.UploadConfig
            )
            .ToList();

        if (
            !importedRelease.IsNewRelease
            || GetContainerScope(importedRelease) is not LinkCrypterContainerScope.ReleaseAllHosters
        )
        {
            return uploadConfigs;
        }

        var archiveFilesUploadConfigs = importedRelease.Release.UploadConfigs.Where(uploadConfig =>
            uploadConfig.UploadSource is UploadSource.ArchiveFiles
        );

        return uploadConfigs.Union(archiveFilesUploadConfigs).ToList();
    }

    private static LinkCrypterContainerScope GetContainerScope(
        LinkCrypterImportedRelease importedRelease
    )
    {
        return importedRelease.MirrorsWithUploadConfigs.Count == 1
            ? LinkCrypterContainerScope.Release
            : LinkCrypterContainerScope.ReleaseAllHosters;
    }

    private static bool HasReleaseCollectionLinkCrypter(
        UploadConfig uploadConfig,
        int linkCrypterRegistrationId
    )
    {
        return uploadConfig.LinkCrypters.Any(linkCrypter =>
            linkCrypter.LinkCrypterRegistrationId == linkCrypterRegistrationId
            && linkCrypter.ContainerScope is LinkCrypterContainerScope.ReleaseCollection
        );
    }

    private static void AddLinkCryptersAndLinkCrypterContainer(
        LinkCrypterImportContainer container,
        LinkCrypterImportedRelease importedRelease,
        List<UploadConfig> uploadConfigsWithLinkCrypter,
        DateTime localNow
    )
    {
        var containerScope = GetContainerScope(importedRelease);

        var linkCrypters = uploadConfigsWithLinkCrypter
            .Select(uploadConfig =>
                GetOrAddLinkCrypter(
                    uploadConfig: uploadConfig,
                    linkCrypterRegistrationId: container
                        .LinkCrypterImport
                        .LinkCrypterRegistrationId,
                    containerScope: containerScope
                )
            )
            .ToList();

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
            linkCrypterContainer.Release = importedRelease.Release;
            linkCrypterContainer.SourceUploads = importedRelease
                .SourceUploads.Select(upload => new LinkCrypterContainerSourceUpload
                {
                    LinkCrypterContainer = linkCrypterContainer,
                    Upload = upload,
                })
                .ToList();

            importedRelease.Release.LinkCrypterContainers.Add(linkCrypterContainer);
            return;
        }

        linkCrypterContainer.UploadConfigLinkCrypter = linkCrypters[0];
        linkCrypterContainer.Upload = importedRelease.SourceUploads.SingleOrDefault();
        linkCrypters[0].LinkCrypterContainers.Add(linkCrypterContainer);
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

    private async Task CompleteImportAsync(
        int linkCrypterImportId,
        int releaseCountWithMovedArchiveParts,
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

        var movedArchivePartsMessage =
            releaseCountWithMovedArchiveParts > 0
                ? $" The archive parts of {releaseCountWithMovedArchiveParts} releases were moved into subfolders named after the release."
                : "";

        linkCrypterImport.ChangeState(LinkCrypterImportState.Completed, timeProvider.GetLocalNow());
        notificationService.Create(
            kind: failedContainerCount == 0
                ? NotificationKind.LinkCrypterImportCompleted
                : NotificationKind.LinkCrypterImportFailed,
            message: $"Link crypter import {linkCrypterImportId} finished: {importedContainerCount} containers were imported, {failedContainerCount} containers failed.{movedArchivePartsMessage}"
        );
        await repository.SaveChangesAsync(cancellationToken);
    }
}
