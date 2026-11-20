using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Exceptions;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Dto;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Importing;

public class CollectionContainerImportService(
    ILinkCrypterImportRepository repository,
    LinkCrypterImportReleaseImportService releaseImportService,
    TimeProvider timeProvider,
    ILogger<CollectionContainerImportService> logger
)
{
    public async Task<List<ArchivePartsMove>> ImportAsync(
        LinkCrypterImportContainer container,
        IReadOnlyList<LinkCrypterImportReleasePreview> releasePreviews,
        CancellationToken cancellationToken
    )
    {
        var localNow = timeProvider.GetLocalNow();

        var archiveStorageFolders = await repository.GetArchiveStorageFoldersAsync(
            cancellationToken
        );

        var importedReleases = new List<LinkCrypterImportedRelease>();

        foreach (var releasePreview in releasePreviews)
        {
            var existingRelease = await repository.GetReleaseForImportByNameOrDefaultAsync(
                releasePreview.ReleaseName,
                cancellationToken
            );

            var importedRelease = await releaseImportService.ImportReleaseAsync(
                linkCrypterImport: container.LinkCrypterImport,
                releasePreview: releasePreview,
                existingRelease: existingRelease,
                archiveStorageFolders: archiveStorageFolders,
                uploadsPostedAt: localNow,
                localNow: localNow,
                cancellationToken: cancellationToken
            );

            importedReleases.Add(importedRelease);
        }

        var releaseCollection = GetReleaseCollectionOfAllReleases(importedReleases);
        var slot = GetCollectionUploadSlotOfAllMirrors(importedReleases);

        var linkCrypterRegistrationId = container.LinkCrypterImport.LinkCrypterRegistrationId;

        AddReleaseCollectionLinkCryptersToSlot(
            slot: slot,
            linkCrypterRegistrationId: linkCrypterRegistrationId,
            settings: CollectionLinkCrypterSync
                .GetSettingsFromSlot(slot)
                .GetValueOrDefault(linkCrypterRegistrationId)
        );

        var linkCrypter = importedReleases
            .SelectMany(importedRelease => importedRelease.MirrorsWithUploadConfigs)
            .First()
            .UploadConfig.LinkCrypters.Single(linkCrypter =>
                linkCrypter.LinkCrypterRegistrationId == linkCrypterRegistrationId
            );

        await AddLinkCrypterContainerUnlessSlotHasItAsync(
            container: container,
            slot: slot,
            linkCrypter: linkCrypter,
            sourceUploads: importedReleases
                .SelectMany(importedRelease => importedRelease.SourceUploads)
                .Distinct()
                .ToList(),
            localNow: localNow,
            cancellationToken: cancellationToken
        );

        releaseCollection.UploadsPostedAt ??= localNow;
        container.ImportedReleaseCollection = releaseCollection;
        container.Status = LinkCrypterImportContainerStatus.Imported;
        container.ErrorMessages = [];

        logger.LogInformation(
            "Imported container {ContainerUrl} into {ReleaseCount} releases of release collection {ReleaseCollectionName}",
            container.ContainerUrl,
            importedReleases.Count,
            releaseCollection.Name
        );

        return importedReleases
            .Select(importedRelease => importedRelease.ArchivePartsMove)
            .OfType<ArchivePartsMove>()
            .ToList();
    }

    private static void AddReleaseCollectionLinkCryptersToSlot(
        CollectionUploadSlot slot,
        int linkCrypterRegistrationId,
        CollectionUploadSlotLinkCrypterSettings? settings
    )
    {
        foreach (var uploadConfig in slot.UploadConfigs)
        {
            GetOrAddReleaseCollectionLinkCrypter(
                uploadConfig: uploadConfig,
                linkCrypterRegistrationId: linkCrypterRegistrationId,
                settings: settings
            );
        }
    }

    private static ReleaseCollection GetReleaseCollectionOfAllReleases(
        List<LinkCrypterImportedRelease> importedReleases
    )
    {
        var firstRelease = importedReleases.First().Release;

        var releaseCollection =
            firstRelease.ReleaseCollection
            ?? throw new InvalidLinkCrypterImportException(
                $"Release {firstRelease.Name} does not belong to a release collection."
            );

        var releaseOfOtherCollection = importedReleases
            .Select(importedRelease => importedRelease.Release)
            .FirstOrDefault(release => release.ReleaseCollection != releaseCollection);

        if (releaseOfOtherCollection is not null)
        {
            throw new InvalidLinkCrypterImportException(
                $"Release {releaseOfOtherCollection.Name} does not belong to release collection {releaseCollection.Name} of release {firstRelease.Name}."
            );
        }

        return releaseCollection;
    }

    private static CollectionUploadSlot GetCollectionUploadSlotOfAllMirrors(
        List<LinkCrypterImportedRelease> importedReleases
    )
    {
        var uploadConfigs = importedReleases
            .SelectMany(importedRelease => importedRelease.MirrorsWithUploadConfigs)
            .Select(mirrorWithUploadConfig => mirrorWithUploadConfig.UploadConfig)
            .ToList();

        if (uploadConfigs.Count == 0)
        {
            throw new InvalidLinkCrypterImportException(
                "No mirror of the container belongs to a hoster of its releases."
            );
        }

        if (uploadConfigs.Any(uploadConfig => uploadConfig.CollectionUploadSlot is null))
        {
            throw new InvalidLinkCrypterImportException(
                "The mirrors of the container do not belong to a collection upload slot."
            );
        }

        var slots = uploadConfigs
            .Select(uploadConfig => uploadConfig.CollectionUploadSlot!)
            .Distinct()
            .ToList();

        if (slots.Count > 1)
        {
            throw new InvalidLinkCrypterImportException(
                "The mirrors of the container belong to different collection upload slots."
            );
        }

        return slots.Single();
    }

    private static void GetOrAddReleaseCollectionLinkCrypter(
        UploadConfig uploadConfig,
        int linkCrypterRegistrationId,
        CollectionUploadSlotLinkCrypterSettings? settings
    )
    {
        var linkCrypter = uploadConfig.LinkCrypters.FirstOrDefault(linkCrypter =>
            linkCrypter.LinkCrypterRegistrationId == linkCrypterRegistrationId
        );

        if (linkCrypter?.ContainerScope is LinkCrypterContainerScope.ReleaseCollection)
        {
            return;
        }

        if (linkCrypter is not null)
        {
            throw new InvalidLinkCrypterImportException(
                $"Upload config {uploadConfig.Name} of release {uploadConfig.ReleaseId} uses this link crypter account for release containers. Change it to the collection container before importing this container."
            );
        }

        linkCrypter = new UploadConfigLinkCrypter
        {
            LinkCrypterRegistrationId = linkCrypterRegistrationId,
            ContainerScope = LinkCrypterContainerScope.ReleaseCollection,
            LinkCrypterContainers = [],
        };

        if (settings is not null)
        {
            linkCrypter.Password = settings.Password;
            linkCrypter.EnableCaptcha = settings.EnableCaptcha;
            linkCrypter.EnableContainerDownload = settings.EnableContainerDownload;
            linkCrypter.EnableClickAndLoad = settings.EnableClickAndLoad;
        }

        uploadConfig.LinkCrypters.Add(linkCrypter);
    }

    private async Task AddLinkCrypterContainerUnlessSlotHasItAsync(
        LinkCrypterImportContainer container,
        CollectionUploadSlot slot,
        UploadConfigLinkCrypter linkCrypter,
        List<Upload> sourceUploads,
        DateTime localNow,
        CancellationToken cancellationToken
    )
    {
        var existingLinkCrypterContainer =
            await repository.GetCollectionLinkCrypterContainerOrDefaultAsync(
                collectionUploadSlotId: slot.Id,
                linkCrypterRegistrationId: linkCrypter.LinkCrypterRegistrationId,
                cancellationToken: cancellationToken
            );

        if (existingLinkCrypterContainer is not null)
        {
            if (existingLinkCrypterContainer.ContainerUrl == container.ContainerUrl)
            {
                return;
            }

            throw new InvalidLinkCrypterImportException(
                $"Collection upload slot {slot.Name} already has container {existingLinkCrypterContainer.ContainerUrl} of this link crypter account."
            );
        }

        var linkCrypterContainer = new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.ReleaseCollection,
            CollectionUploadSlot = slot,
            LinkCrypterRegistrationId = linkCrypter.LinkCrypterRegistrationId,
            ContainerUrl = container.ContainerUrl,
            ExternalReference = container.ExternalReference,
            State = LinkCrypterContainerState.Created,
            Errors = [],
            CreatedAt = localNow,
            Notifications = [],
        };

        linkCrypterContainer.CopySettingsFrom(linkCrypter);
        linkCrypterContainer.SourceUploads = sourceUploads
            .Select(upload => new LinkCrypterContainerSourceUpload
            {
                LinkCrypterContainer = linkCrypterContainer,
                Upload = upload,
            })
            .ToList();

        repository.Add(linkCrypterContainer);
    }
}
