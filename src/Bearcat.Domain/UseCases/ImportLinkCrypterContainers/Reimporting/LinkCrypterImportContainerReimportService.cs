using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.CollectionAssignment;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Exceptions;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Importing;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Dto;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Reimporting;

public class LinkCrypterImportContainerReimportService(
    ILinkCrypterImportRepository repository,
    IReleaseWriteRepository releaseWriteRepository,
    LinkCrypterImportPreviewService previewService,
    CollectionContainerImportService collectionContainerImportService,
    IReleaseCollectionAssigner releaseCollectionAssigner,
    ILogger<LinkCrypterImportContainerReimportService> logger
)
{
    private static readonly UploadState[] UploadStatesInProgress =
    [
        UploadState.WaitingForArchive,
        UploadState.Pending,
        UploadState.Uploading,
        UploadState.CancellationRequested,
    ];

    private static readonly ArchiveState[] ArchiveStatesInProgress =
    [
        ArchiveState.Creating,
        ArchiveState.Restoring,
    ];

    public async Task<LinkCrypterImportReimportPlanReadModel> CreatePlanAsync(
        int linkCrypterImportContainerId,
        CancellationToken cancellationToken = default
    )
    {
        var plan = await ApplyReimportWithoutSavingAsync(
            linkCrypterImportContainerId,
            cancellationToken
        );

        repository.ClearChangeTracker();
        releaseCollectionAssigner.ClearCachedReleaseCollections();

        return plan;
    }

    public async Task ReimportAsync(
        int linkCrypterImportContainerId,
        CancellationToken cancellationToken = default
    )
    {
        var plan = await ApplyReimportWithoutSavingAsync(
            linkCrypterImportContainerId,
            cancellationToken
        );

        if (plan.Blockers.Count > 0)
        {
            throw new InvalidLinkCrypterImportException(
                $"Container {plan.ContainerUrl} cannot be re-imported: {string.Join("; ", plan.Blockers.Select(DescribeBlocker))}"
            );
        }

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Re-imported container {ContainerUrl} of release {ReleaseName} into {ReleaseCount} releases of release collection {ReleaseCollectionName}",
            plan.ContainerUrl,
            plan.ImportedReleaseName,
            plan.Releases.Count,
            plan.ReleaseCollectionName
        );
    }

    private async Task<LinkCrypterImportReimportPlanReadModel> ApplyReimportWithoutSavingAsync(
        int linkCrypterImportContainerId,
        CancellationToken cancellationToken
    )
    {
        var container = await repository.GetContainerWithImportAsync(
            linkCrypterImportContainerId,
            cancellationToken
        );

        if (
            container.Status is not LinkCrypterImportContainerStatus.Imported
            || container.ImportedReleaseId is null
        )
        {
            throw new InvalidLinkCrypterImportException(
                $"Container {container.ContainerUrl} was not imported into a release."
            );
        }

        var release = await repository.GetReleaseForReimportAsync(
            container.ImportedReleaseId.Value,
            cancellationToken
        );

        var previewResult = await previewService.CreatePreviewOfContainerAsync(
            container,
            cancellationToken
        );

        var releasePreviews = previewResult.Preview.Releases!;

        EnsureReleasePreviewsReplaceRelease(container, release, releasePreviews);

        var blockers = GetBlockers(release, previewResult);
        var uploadedFilesOfArchives = GetCompletedUploadedFilesOfArchives(release);
        var uploadsNotMovedBeforeImport = GetUploadsNotMovedBeforeImport(
            uploadedFilesOfArchives,
            releasePreviews
        );

        if (
            blockers.Any(blocker =>
                blocker.Reason is LinkCrypterImportReimportBlockerReason.NotImportable
            )
        )
        {
            return CreatePlan(
                container: container,
                release: release,
                preview: previewResult.Preview,
                importedReleases: [],
                uploadsNotMovedBeforeImport: uploadsNotMovedBeforeImport,
                containerConversion: new LinkCrypterContainerConversion([], []),
                blockers: blockers
            );
        }

        container.PreviewJson = previewResult.Preview.ToJson();

        CollectionContainerImportResult importResult;

        try
        {
            importResult = await collectionContainerImportService.ImportAsync(
                container: container,
                releasePreviews: releasePreviews,
                uploadsToMove: GetUploadsToMove(uploadedFilesOfArchives, releasePreviews),
                uploadsPostedAt: release.UploadsPostedAt,
                cancellationToken: cancellationToken
            );
        }
        catch (InvalidLinkCrypterImportException exception)
        {
            return CreatePlan(
                container: container,
                release: release,
                preview: previewResult.Preview,
                importedReleases: [],
                uploadsNotMovedBeforeImport: uploadsNotMovedBeforeImport,
                containerConversion: new LinkCrypterContainerConversion([], []),
                blockers:
                [
                    .. blockers,
                    new LinkCrypterImportReimportBlocker(
                        Reason: LinkCrypterImportReimportBlockerReason.ImportFailed,
                        Detail: exception.Message,
                        NotImportableReason: null
                    ),
                ]
            );
        }

        var containerConversion = await ConvertLinkCrypterContainersOfReleaseAsync(
            container: container,
            release: release,
            importResult: importResult,
            cancellationToken: cancellationToken
        );

        await AssignReleaseCollectionToOtherImportContainersAsync(
            container: container,
            release: release,
            releaseCollection: importResult.ReleaseCollection,
            convertedContainerUrls: containerConversion.ConvertedContainerUrls,
            cancellationToken: cancellationToken
        );

        MovePostedLocationsToReleaseCollection(release, importResult.ReleaseCollection);
        RemoveReleaseWithoutMovedEntities(release);

        return CreatePlan(
            container: container,
            release: release,
            preview: previewResult.Preview,
            importedReleases: importResult.ImportedReleases,
            uploadsNotMovedBeforeImport: uploadsNotMovedBeforeImport,
            containerConversion: containerConversion,
            blockers: blockers
        );
    }

    private static void EnsureReleasePreviewsReplaceRelease(
        LinkCrypterImportContainer container,
        Release release,
        IReadOnlyList<LinkCrypterImportReleasePreview> releasePreviews
    )
    {
        if (releasePreviews.Count < 2)
        {
            throw new InvalidLinkCrypterImportException(
                $"Container {container.ContainerUrl} does not contain the archives of several releases."
            );
        }

        if (releasePreviews.Any(releasePreview => releasePreview.ExistingReleaseId == release.Id))
        {
            throw new InvalidLinkCrypterImportException(
                $"Release {release.Name} has the name of a release of container {container.ContainerUrl}."
            );
        }
    }

    private static List<LinkCrypterImportReimportBlocker> GetBlockers(
        Release release,
        LinkCrypterImportContainerPreviewResult previewResult
    )
    {
        var uploadBlockers = release
            .UploadConfigs.Where(uploadConfig =>
                uploadConfig.Uploads.Any(upload =>
                    UploadStatesInProgress.Contains(upload.UploadState)
                )
            )
            .Select(uploadConfig => new LinkCrypterImportReimportBlocker(
                Reason: LinkCrypterImportReimportBlockerReason.UploadInProgress,
                Detail: uploadConfig.Name,
                NotImportableReason: null
            ));

        var archiveBlockers = release
            .ArchiveConfigs.Where(archiveConfig =>
                archiveConfig.Archives.Any(archive =>
                    ArchiveStatesInProgress.Contains(archive.ArchiveState)
                )
            )
            .Select(archiveConfig => new LinkCrypterImportReimportBlocker(
                Reason: LinkCrypterImportReimportBlockerReason.ArchiveInProgress,
                Detail: archiveConfig.Name,
                NotImportableReason: null
            ));

        List<LinkCrypterImportReimportBlocker> containerBlockers =
            previewResult.ContainerNotImportableReason is { } containerNotImportableReason
                ?
                [
                    new LinkCrypterImportReimportBlocker(
                        Reason: LinkCrypterImportReimportBlockerReason.NotImportable,
                        Detail: null,
                        NotImportableReason: containerNotImportableReason
                    ),
                ]
                : [];

        var releaseBlockers = previewResult
            .Preview.Releases!.Where(releasePreview =>
                releasePreview.NotImportableReason is not null
            )
            .Select(releasePreview => new LinkCrypterImportReimportBlocker(
                Reason: LinkCrypterImportReimportBlockerReason.NotImportable,
                Detail: releasePreview.ReleaseName,
                NotImportableReason: releasePreview.NotImportableReason
            ));

        return [.. uploadBlockers, .. archiveBlockers, .. containerBlockers, .. releaseBlockers];
    }

    private static List<UploadedFilesOfArchive> GetCompletedUploadedFilesOfArchives(Release release)
    {
        return release
            .UploadConfigs.SelectMany(uploadConfig => uploadConfig.Uploads)
            .Where(upload => upload.UploadState is UploadState.Completed)
            .OrderBy(upload => upload.Id)
            .SelectMany(upload =>
                upload
                    .UploadedFiles.OrderBy(uploadedFile => uploadedFile.Id)
                    .GroupBy(GetArchiveBaseNameOrDefault, StringComparer.OrdinalIgnoreCase)
                    .Select(group => new UploadedFilesOfArchive(
                        Upload: upload,
                        ArchiveBaseName: group.Key,
                        UploadedFiles: group.ToList()
                    ))
            )
            .ToList();
    }

    private static string? GetArchiveBaseNameOrDefault(UploadedFile uploadedFile)
    {
        if (uploadedFile.ArchiveFile is null)
        {
            return null;
        }

        var fileName = Path.GetFileName(uploadedFile.ArchiveFile.FullFileName);

        return ArchivePartFileNames.GetArchiveBaseName(fileName) ?? fileName;
    }

    private static List<LinkCrypterImportUploadToMove> GetUploadsToMove(
        List<UploadedFilesOfArchive> uploadedFilesOfArchives,
        IReadOnlyList<LinkCrypterImportReleasePreview> releasePreviews
    )
    {
        return uploadedFilesOfArchives
            .Where(uploadedFilesOfArchive =>
                IsReleaseOfContainer(uploadedFilesOfArchive.ArchiveBaseName, releasePreviews)
            )
            .Select(uploadedFilesOfArchive => new LinkCrypterImportUploadToMove(
                ReleaseName: uploadedFilesOfArchive.ArchiveBaseName!,
                OriginalUpload: uploadedFilesOfArchive.Upload,
                UploadedFiles: uploadedFilesOfArchive.UploadedFiles
            ))
            .ToList();
    }

    private static List<LinkCrypterImportReimportUploadNotMovedReadModel> GetUploadsNotMovedBeforeImport(
        List<UploadedFilesOfArchive> uploadedFilesOfArchives,
        IReadOnlyList<LinkCrypterImportReleasePreview> releasePreviews
    )
    {
        return uploadedFilesOfArchives
            .Where(uploadedFilesOfArchive =>
                !IsReleaseOfContainer(uploadedFilesOfArchive.ArchiveBaseName, releasePreviews)
            )
            .Select(uploadedFilesOfArchive => new LinkCrypterImportReimportUploadNotMovedReadModel(
                UploadConfigName: uploadedFilesOfArchive.Upload.UploadConfig.Name,
                ArchiveBaseName: uploadedFilesOfArchive.ArchiveBaseName,
                FileCount: uploadedFilesOfArchive.UploadedFiles.Count,
                Reason: uploadedFilesOfArchive.ArchiveBaseName is null
                    ? LinkCrypterImportUploadNotMovedReason.UploadedFileWithoutArchiveFile
                    : LinkCrypterImportUploadNotMovedReason.NoReleaseWithArchiveName
            ))
            .ToList();
    }

    private static bool IsReleaseOfContainer(
        string? archiveBaseName,
        IReadOnlyList<LinkCrypterImportReleasePreview> releasePreviews
    )
    {
        return releasePreviews.Any(releasePreview =>
            string.Equals(
                releasePreview.ReleaseName,
                archiveBaseName,
                StringComparison.OrdinalIgnoreCase
            )
        );
    }

    private async Task<LinkCrypterContainerConversion> ConvertLinkCrypterContainersOfReleaseAsync(
        LinkCrypterImportContainer container,
        Release release,
        CollectionContainerImportResult importResult,
        CancellationToken cancellationToken
    )
    {
        var slot = importResult.CollectionUploadSlot;

        var newUploadsByOriginalUpload = importResult
            .ImportedReleases.SelectMany(importedRelease => importedRelease.UploadMoveResults)
            .Where(uploadMoveResult => uploadMoveResult.NewUpload is not null)
            .ToLookup(
                uploadMoveResult => uploadMoveResult.UploadToMove.OriginalUpload,
                uploadMoveResult => uploadMoveResult.NewUpload!
            );

        var linkCrypterRegistrationIdsWithSlotContainer = new HashSet<int>
        {
            container.LinkCrypterImport.LinkCrypterRegistrationId,
        };

        var conversion = new LinkCrypterContainerConversion([], []);

        foreach (
            var linkCrypterContainer in GetLinkCrypterContainers(release)
                .Where(linkCrypterContainer =>
                    linkCrypterContainer.ContainerUrl != container.ContainerUrl
                )
        )
        {
            var notConvertedReason = await GetNotConvertedReasonOrDefaultAsync(
                linkCrypterContainer: linkCrypterContainer,
                slot: slot,
                linkCrypterRegistrationIdsWithSlotContainer: linkCrypterRegistrationIdsWithSlotContainer,
                cancellationToken: cancellationToken
            );

            if (notConvertedReason is not null)
            {
                conversion.ContainersNotConverted.Add(
                    new LinkCrypterImportReimportContainerNotConvertedReadModel(
                        ContainerUrl: linkCrypterContainer.ContainerUrl,
                        Reason: notConvertedReason.Value
                    )
                );

                continue;
            }

            ConvertToReleaseCollectionContainer(
                linkCrypterContainer: linkCrypterContainer,
                release: release,
                slot: slot,
                newUploadsByOriginalUpload: newUploadsByOriginalUpload
            );

            linkCrypterRegistrationIdsWithSlotContainer.Add(
                linkCrypterContainer.LinkCrypterRegistrationId
            );

            conversion.ConvertedContainerUrls.Add(linkCrypterContainer.ContainerUrl);
        }

        return conversion;
    }

    private static List<LinkCrypterContainer> GetLinkCrypterContainers(Release release)
    {
        return release
            .LinkCrypterContainers.Concat(
                release
                    .UploadConfigs.SelectMany(uploadConfig => uploadConfig.LinkCrypters)
                    .SelectMany(linkCrypter => linkCrypter.LinkCrypterContainers)
            )
            .OrderBy(linkCrypterContainer => linkCrypterContainer.Id)
            .ToList();
    }

    private async Task<LinkCrypterImportReimportContainerNotConvertedReason?> GetNotConvertedReasonOrDefaultAsync(
        LinkCrypterContainer linkCrypterContainer,
        CollectionUploadSlot slot,
        HashSet<int> linkCrypterRegistrationIdsWithSlotContainer,
        CancellationToken cancellationToken
    )
    {
        var linkCrypterRegistrationId = linkCrypterContainer.LinkCrypterRegistrationId;

        if (linkCrypterRegistrationIdsWithSlotContainer.Contains(linkCrypterRegistrationId))
        {
            return LinkCrypterImportReimportContainerNotConvertedReason.CollectionUploadSlotHasContainerOfLinkCrypterAccount;
        }

        var existingCollectionContainer =
            await repository.GetCollectionLinkCrypterContainerOrDefaultAsync(
                collectionUploadSlotId: slot.Id,
                linkCrypterRegistrationId: linkCrypterRegistrationId,
                cancellationToken: cancellationToken
            );

        if (existingCollectionContainer is not null)
        {
            return LinkCrypterImportReimportContainerNotConvertedReason.CollectionUploadSlotHasContainerOfLinkCrypterAccount;
        }

        var slotUsesLinkCrypterAccountForReleaseContainers = slot
            .UploadConfigs.SelectMany(uploadConfig => uploadConfig.LinkCrypters)
            .Any(linkCrypter =>
                linkCrypter.LinkCrypterRegistrationId == linkCrypterRegistrationId
                && linkCrypter.ContainerScope is not LinkCrypterContainerScope.ReleaseCollection
            );

        return slotUsesLinkCrypterAccountForReleaseContainers
            ? LinkCrypterImportReimportContainerNotConvertedReason.CollectionUploadSlotUsesLinkCrypterAccountForReleaseContainers
            : null;
    }

    private static void ConvertToReleaseCollectionContainer(
        LinkCrypterContainer linkCrypterContainer,
        Release release,
        CollectionUploadSlot slot,
        ILookup<Upload, Upload> newUploadsByOriginalUpload
    )
    {
        var linkCrypterRegistrationId = linkCrypterContainer.LinkCrypterRegistrationId;

        CollectionContainerImportService.AddReleaseCollectionLinkCryptersToSlot(
            slot: slot,
            linkCrypterRegistrationId: linkCrypterRegistrationId,
            settings: CollectionLinkCrypterSync
                .GetSettingsFromSlot(slot)
                .GetValueOrDefault(linkCrypterRegistrationId)
                ?? new CollectionUploadSlotLinkCrypterSettings(
                    LinkCrypterRegistrationId: linkCrypterRegistrationId,
                    Password: linkCrypterContainer.Password,
                    EnableCaptcha: linkCrypterContainer.EnableCaptcha,
                    EnableContainerDownload: linkCrypterContainer.EnableContainerDownload,
                    EnableClickAndLoad: linkCrypterContainer.EnableClickAndLoad
                )
        );

        var newSourceUploads = linkCrypterContainer
            .SourceUploads.Select(sourceUpload => sourceUpload.Upload)
            .Append(linkCrypterContainer.Upload)
            .OfType<Upload>()
            .Distinct()
            .SelectMany(originalUpload => newUploadsByOriginalUpload[originalUpload])
            .ToList();

        release.LinkCrypterContainers.Remove(linkCrypterContainer);

        linkCrypterContainer.UploadConfigLinkCrypter?.LinkCrypterContainers.Remove(
            linkCrypterContainer
        );

        linkCrypterContainer.Upload?.LinkCrypterContainers.Remove(linkCrypterContainer);

        linkCrypterContainer.Scope = LinkCrypterContainerScope.ReleaseCollection;
        linkCrypterContainer.CollectionUploadSlot = slot;
        linkCrypterContainer.Release = null;
        linkCrypterContainer.ReleaseId = null;
        linkCrypterContainer.UploadConfigLinkCrypter = null;
        linkCrypterContainer.UploadConfigLinkCrypterId = null;
        linkCrypterContainer.Upload = null;
        linkCrypterContainer.UploadId = null;

        linkCrypterContainer.SourceUploads = newSourceUploads
            .Select(upload => new LinkCrypterContainerSourceUpload
            {
                LinkCrypterContainer = linkCrypterContainer,
                Upload = upload,
            })
            .ToList();
    }

    private async Task AssignReleaseCollectionToOtherImportContainersAsync(
        LinkCrypterImportContainer container,
        Release release,
        ReleaseCollection releaseCollection,
        List<string> convertedContainerUrls,
        CancellationToken cancellationToken
    )
    {
        var importContainersOfRelease = await repository.GetContainersImportedIntoReleaseAsync(
            release.Id,
            cancellationToken
        );

        foreach (
            var importContainer in importContainersOfRelease.Where(importContainer =>
                importContainer.Id != container.Id
                && (
                    importContainer.ContainerUrl == container.ContainerUrl
                    || convertedContainerUrls.Contains(importContainer.ContainerUrl)
                )
            )
        )
        {
            importContainer.ImportedReleaseCollection = releaseCollection;
        }
    }

    private static void MovePostedLocationsToReleaseCollection(
        Release release,
        ReleaseCollection releaseCollection
    )
    {
        foreach (var postedLocation in release.PostedLocations)
        {
            postedLocation.Release = null;
            postedLocation.ReleaseId = null;
            postedLocation.ReleaseCollection = releaseCollection;
        }

        release.PostedLocations.Clear();
    }

    private void RemoveReleaseWithoutMovedEntities(Release release)
    {
        repository.DetectChanges();
        releaseWriteRepository.Remove(release);
    }

    private static LinkCrypterImportReimportPlanReadModel CreatePlan(
        LinkCrypterImportContainer container,
        Release release,
        LinkCrypterImportContainerPreview preview,
        IReadOnlyList<LinkCrypterImportedRelease> importedReleases,
        List<LinkCrypterImportReimportUploadNotMovedReadModel> uploadsNotMovedBeforeImport,
        LinkCrypterContainerConversion containerConversion,
        IReadOnlyList<LinkCrypterImportReimportBlocker> blockers
    )
    {
        var uploadMoveResults = importedReleases
            .SelectMany(importedRelease => importedRelease.UploadMoveResults)
            .ToList();

        var uploadsNotMovedDuringImport = uploadMoveResults
            .Where(uploadMoveResult => uploadMoveResult.NotMovedReason is not null)
            .Select(uploadMoveResult => new LinkCrypterImportReimportUploadNotMovedReadModel(
                UploadConfigName: uploadMoveResult.UploadToMove.OriginalUpload.UploadConfig.Name,
                ArchiveBaseName: uploadMoveResult.UploadToMove.ReleaseName,
                FileCount: uploadMoveResult.UploadToMove.UploadedFiles.Count,
                Reason: uploadMoveResult.NotMovedReason!.Value
            ));

        return new LinkCrypterImportReimportPlanReadModel(
            ContainerUrl: container.ContainerUrl,
            ImportedReleaseName: release.Name,
            ReleaseCollectionName: preview.ReleaseCollectionName,
            Releases: preview
                .Releases!.Select(releasePreview => new LinkCrypterImportReimportReleaseReadModel(
                    Preview: releasePreview,
                    MovedUploads: GetMovedUploads(uploadMoveResults, releasePreview.ReleaseName)
                ))
                .ToList(),
            UploadsNotMoved: [.. uploadsNotMovedBeforeImport, .. uploadsNotMovedDuringImport],
            ConvertedContainerUrls: containerConversion.ConvertedContainerUrls,
            ContainersNotConverted: containerConversion.ContainersNotConverted,
            Blockers: blockers
        );
    }

    private static List<LinkCrypterImportReimportMovedUploadReadModel> GetMovedUploads(
        List<LinkCrypterImportUploadMoveResult> uploadMoveResults,
        string releaseName
    )
    {
        return uploadMoveResults
            .Where(uploadMoveResult =>
                uploadMoveResult.NewUpload is not null
                && string.Equals(
                    uploadMoveResult.UploadToMove.ReleaseName,
                    releaseName,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .Select(uploadMoveResult => new LinkCrypterImportReimportMovedUploadReadModel(
                UploadConfigName: uploadMoveResult.NewUpload!.UploadConfig.Name,
                FileCount: uploadMoveResult.NewUpload.UploadedFiles.Count
            ))
            .ToList();
    }

    private static string DescribeBlocker(LinkCrypterImportReimportBlocker blocker)
    {
        List<string?> parts =
        [
            blocker.Reason.ToString(),
            blocker.Detail,
            blocker.NotImportableReason?.ToString(),
        ];

        return string.Join(" ", parts.OfType<string>());
    }

    private sealed record UploadedFilesOfArchive(
        Upload Upload,
        string? ArchiveBaseName,
        List<UploadedFile> UploadedFiles
    );

    private sealed record LinkCrypterContainerConversion(
        List<string> ConvertedContainerUrls,
        List<LinkCrypterImportReimportContainerNotConvertedReadModel> ContainersNotConverted
    );
}
