using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
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
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Reimporting;

public class LinkCrypterImportContainerReimportService(
    ILinkCrypterImportRepository repository,
    IReleaseWriteRepository releaseWriteRepository,
    LinkCrypterImportPreviewService previewService,
    CollectionContainerImportService collectionContainerImportService,
    ArchivePartMover archivePartMover,
    IReleaseCollectionAssigner releaseCollectionAssigner,
    INotificationService notificationService,
    TimeProvider timeProvider,
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
        var appliedReimport = await ApplyReimportOfSingleContainerWithoutSavingAsync(
            linkCrypterImportContainerId,
            cancellationToken
        );

        repository.ClearChangeTracker();
        releaseCollectionAssigner.ClearCachedReleaseCollections();

        return appliedReimport.Plan;
    }

    public async Task<LinkCrypterImportReimportPlanReadModel> ReimportAsync(
        int linkCrypterImportContainerId,
        CancellationToken cancellationToken = default
    )
    {
        var (plan, archivePartsMoves) = await ApplyReimportOfSingleContainerWithoutSavingAsync(
            linkCrypterImportContainerId,
            cancellationToken
        );

        if (plan.Blockers.Count > 0)
        {
            throw new InvalidLinkCrypterImportException(DescribeBlockers(plan));
        }

        await MoveArchivePartsAndSaveAsync(plan, archivePartsMoves, cancellationToken);

        return plan;
    }

    public async Task ReimportSelectedContainersAsync(
        int linkCrypterImportId,
        CancellationToken cancellationToken
    )
    {
        var linkCrypterImport = await repository.GetWithContainersAsync(
            linkCrypterImportId,
            cancellationToken
        );

        var containerIds = linkCrypterImport
            .Containers.Where(container =>
                container.IsSelected
                && LinkCrypterImportContainerReimportCheck.CanBeReimported(container)
            )
            .OrderBy(container => container.Id)
            .Select(container => container.Id)
            .ToList();

        linkCrypterImport.StartProgress(containerIds.Count);
        await repository.SaveChangesAsync(cancellationToken);

        var previewRun = await previewService.CreatePreviewRunAsync(
            linkCrypterImport,
            cancellationToken
        );

        var reimportedContainerCount = 0;
        var releaseCountWithMovedArchiveParts = 0;

        foreach (var containerId in containerIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var containerReimport = await ReimportContainerOrSaveErrorAsync(
                linkCrypterImportContainerId: containerId,
                previewRun: previewRun,
                cancellationToken: cancellationToken
            );

            if (containerReimport.IsReimported)
            {
                reimportedContainerCount++;

                releaseCountWithMovedArchiveParts +=
                    containerReimport.ReleaseCountWithMovedArchiveParts;
            }
        }

        await CompleteReimportAsync(
            linkCrypterImportId: linkCrypterImportId,
            reimportedContainerCount: reimportedContainerCount,
            failedContainerCount: containerIds.Count - reimportedContainerCount,
            releaseCountWithMovedArchiveParts: releaseCountWithMovedArchiveParts,
            cancellationToken: cancellationToken
        );
    }

    private async Task<ContainerReimport> ReimportContainerOrSaveErrorAsync(
        int linkCrypterImportContainerId,
        LinkCrypterImportPreviewRun previewRun,
        CancellationToken cancellationToken
    )
    {
        repository.ClearChangeTracker();
        releaseCollectionAssigner.ClearCachedReleaseCollections();

        string? errorMessage = null;
        var releaseCountWithMovedArchiveParts = 0;

        try
        {
            var container = await repository.GetContainerWithImportAsync(
                linkCrypterImportContainerId,
                cancellationToken
            );

            var (plan, archivePartsMoves) = await ApplyReimportWithoutSavingAsync(
                container: container,
                previewRun: previewRun,
                cancellationToken: cancellationToken
            );

            if (plan.Blockers.Count > 0)
            {
                errorMessage = DescribeBlockers(plan);
            }
            else
            {
                await MoveArchivePartsAndSaveAsync(plan, archivePartsMoves, cancellationToken);
                releaseCountWithMovedArchiveParts = archivePartsMoves.Count;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Could not re-import container {LinkCrypterImportContainerId}",
                linkCrypterImportContainerId
            );

            errorMessage = exception.Message;
        }

        if (errorMessage is not null)
        {
            repository.ClearChangeTracker();
        }

        var reloadedContainer = await repository.GetContainerWithImportAsync(
            linkCrypterImportContainerId,
            cancellationToken
        );

        reloadedContainer.ErrorMessages = errorMessage is null ? [] : [errorMessage];
        reloadedContainer.LinkCrypterImport.ProcessedContainerCount++;
        await repository.SaveChangesAsync(cancellationToken);

        return new ContainerReimport(
            IsReimported: errorMessage is null,
            ReleaseCountWithMovedArchiveParts: releaseCountWithMovedArchiveParts
        );
    }

    private async Task MoveArchivePartsAndSaveAsync(
        LinkCrypterImportReimportPlanReadModel plan,
        IReadOnlyList<ArchivePartsMove> archivePartsMoves,
        CancellationToken cancellationToken
    )
    {
        archivePartMover.MoveArchiveParts(archivePartsMoves);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Re-imported container {ContainerUrl} of release {ReleaseName} into {ReleaseCount} releases of release collection {ReleaseCollectionName}",
            plan.ContainerUrl,
            plan.ImportedReleaseName,
            plan.Releases.Count,
            plan.ReleaseCollectionName
        );
    }

    private async Task CompleteReimportAsync(
        int linkCrypterImportId,
        int reimportedContainerCount,
        int failedContainerCount,
        int releaseCountWithMovedArchiveParts,
        CancellationToken cancellationToken
    )
    {
        repository.ClearChangeTracker();

        var linkCrypterImport = await repository.GetByIdAsync(
            linkCrypterImportId,
            cancellationToken
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
            message: $"Re-import of link crypter import {linkCrypterImportId} finished: {reimportedContainerCount} containers were re-imported, {failedContainerCount} containers failed.{movedArchivePartsMessage}"
        );

        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<AppliedReimport> ApplyReimportOfSingleContainerWithoutSavingAsync(
        int linkCrypterImportContainerId,
        CancellationToken cancellationToken
    )
    {
        var container = await repository.GetContainerWithImportAsync(
            linkCrypterImportContainerId,
            cancellationToken
        );

        var previewRun = await previewService.CreatePreviewRunAsync(
            container.LinkCrypterImport,
            cancellationToken
        );

        return await ApplyReimportWithoutSavingAsync(
            container: container,
            previewRun: previewRun,
            cancellationToken: cancellationToken
        );
    }

    private async Task<AppliedReimport> ApplyReimportWithoutSavingAsync(
        LinkCrypterImportContainer container,
        LinkCrypterImportPreviewRun previewRun,
        CancellationToken cancellationToken
    )
    {
        if (!LinkCrypterImportContainerReimportCheck.CanBeReimported(container))
        {
            throw new InvalidLinkCrypterImportException(
                $"Container {container.ContainerUrl} cannot be re-imported."
            );
        }

        var release = await repository.GetReleaseForReimportAsync(
            container.ImportedReleaseId!.Value,
            cancellationToken
        );

        var previewResult = await previewService.CreatePreviewOfContainerAsync(
            run: previewRun,
            container: container,
            folderPathsOfReplacedRelease: GetFolderPathsOfArchivesThatAreNotDeleted(release),
            cancellationToken: cancellationToken
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
            return new AppliedReimport(
                Plan: CreatePlan(
                    container: container,
                    release: release,
                    preview: previewResult.Preview,
                    importedReleases: [],
                    uploadsNotMovedBeforeImport: uploadsNotMovedBeforeImport,
                    containerConversion: new LinkCrypterContainerConversion([], []),
                    blockers: blockers
                ),
                ArchivePartsMoves: []
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
            return new AppliedReimport(
                Plan: CreatePlan(
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
                ),
                ArchivePartsMoves: []
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

        return new AppliedReimport(
            Plan: CreatePlan(
                container: container,
                release: release,
                preview: previewResult.Preview,
                importedReleases: importResult.ImportedReleases,
                uploadsNotMovedBeforeImport: uploadsNotMovedBeforeImport,
                containerConversion: containerConversion,
                blockers: blockers
            ),
            ArchivePartsMoves: importResult.ArchivePartsMoves
        );
    }

    private static List<string> GetFolderPathsOfArchivesThatAreNotDeleted(Release release)
    {
        return release
            .ArchiveConfigs.SelectMany(archiveConfig => archiveConfig.Archives)
            .Where(archive => archive.ArchiveState is not ArchiveState.Deleted)
            .Select(archive => archive.ArchiveFolderPath)
            .Distinct(StringComparer.Ordinal)
            .ToList();
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

    private static string DescribeBlockers(LinkCrypterImportReimportPlanReadModel plan)
    {
        return $"Container {plan.ContainerUrl} cannot be re-imported: {string.Join("; ", plan.Blockers.Select(DescribeBlocker))}";
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

    private sealed record AppliedReimport(
        LinkCrypterImportReimportPlanReadModel Plan,
        IReadOnlyList<ArchivePartsMove> ArchivePartsMoves
    );

    private sealed record ContainerReimport(
        bool IsReimported,
        int ReleaseCountWithMovedArchiveParts
    );

    private sealed record LinkCrypterContainerConversion(
        List<string> ConvertedContainerUrls,
        List<LinkCrypterImportReimportContainerNotConvertedReadModel> ContainersNotConverted
    );
}
