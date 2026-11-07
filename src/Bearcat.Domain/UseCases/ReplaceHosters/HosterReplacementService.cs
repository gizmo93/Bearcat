using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;
using Bearcat.Domain.UseCases.ReplaceHosters.Dto;
using Bearcat.Domain.UseCases.ReplaceHosters.ReadModels;
using Bearcat.Domain.UseCases.ReplaceHosters.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ReplaceHosters;

public class HosterReplacementService(
    IHosterReplacementRepository repository,
    IHosterFactory hosterFactory,
    TimeProvider timeProvider,
    ILogger<HosterReplacementService> logger
)
{
    public async Task<IReadOnlyList<HosterReplacementSourceChoiceReadModel>> GetSourceChoicesAsync(
        string hosterClassName,
        CancellationToken cancellationToken = default
    )
    {
        return await repository.GetSourceChoicesAsync(hosterClassName, cancellationToken);
    }

    public async Task<HosterReplacementPreview> GetPreviewAsync(
        HosterReplacementRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var plan = await CreatePlanAsync(request, cancellationToken);

        return await CreatePreviewAsync(request, plan, cancellationToken);
    }

    public async Task<HosterReplacementPreview> ExecuteAsync(
        HosterReplacementRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var plan = await CreatePlanAsync(request, cancellationToken);
        var preview = await CreatePreviewAsync(request, plan, cancellationToken);

        var appliedChangeCounts = await repository.ApplyChangesAndGetChangeCountsAsync(
            plan.Changes,
            cancellationToken
        );

        logger.LogInformation(
            "Replaced hoster {SourceHosterClassName} with upload target {ReplacementUploadTargetName}: {UploadConfigCount} upload configs, {UploadConfigTemplateCount} upload config templates, {MovedUploadCount} unstarted uploads, {DeactivatedAccountCount} accounts deactivated, {UploadCountMarkedOffline} uploads marked offline, {SkippedUploadConfigCount} upload configs and {SkippedUploadConfigTemplateCount} upload config templates skipped",
            plan.SourceHosterClassName,
            plan.Changes.ReplacementUploadTargetName,
            preview.UploadConfigCount,
            preview.UploadConfigTemplateCount,
            appliedChangeCounts.MovedUploadCount,
            appliedChangeCounts.DeactivatedAccountCount,
            appliedChangeCounts.UploadCountMarkedOffline,
            preview.SkippedUploadConfigs.Count,
            preview.SkippedUploadConfigTemplates.Count
        );

        return preview with
        {
            UnstartedUploadCount = appliedChangeCounts.MovedUploadCount,
            AccountCountToDeactivate = appliedChangeCounts.DeactivatedAccountCount,
            UploadCountToMarkOffline = appliedChangeCounts.UploadCountMarkedOffline,
        };
    }

    private async Task<HosterReplacementPlan> CreatePlanAsync(
        HosterReplacementRequest request,
        CancellationToken cancellationToken
    )
    {
        EnsureSourcesAreDistinctAndDifferFromReplacement(request);

        var uploadTargets = await repository.GetUploadTargetsAsync(
            [.. request.SourceUploadTargets, request.ReplacementUploadTarget],
            cancellationToken
        );
        var uploadTargetsByUploadTarget = uploadTargets.ToDictionary(target => target.UploadTarget);
        var sources = request
            .SourceUploadTargets.Select(target => uploadTargetsByUploadTarget[target])
            .ToList();
        var replacement = uploadTargetsByUploadTarget[request.ReplacementUploadTarget];

        EnsureReplacementCanReplaceSources(sources, replacement);
        EnsureDeadSourceHosterDiffersFromReplacementHoster(request, sources, replacement);

        var sourceNamesByUploadTarget = sources.ToDictionary(
            source => source.UploadTarget,
            source => source.Name
        );

        var uploadConfigRows =
            await repository.GetUploadConfigRowsOfReleasesContainingUploadTargetsAsync(
                request.SourceUploadTargets,
                cancellationToken
            );
        var uploadConfigTemplateRows =
            await repository.GetUploadConfigTemplateRowsOfReleaseTemplatesContainingUploadTargetsAsync(
                request.SourceUploadTargets,
                cancellationToken
            );

        var sourceUploadConfigRows = uploadConfigRows
            .Where(row => sourceNamesByUploadTarget.ContainsKey(row.UploadTarget))
            .ToList();
        var sourceUploadConfigTemplateRows = uploadConfigTemplateRows
            .Where(row => sourceNamesByUploadTarget.ContainsKey(row.UploadTarget))
            .ToList();

        var skipReasonsByUploadConfigId = GetSkipReasonsByUploadConfigId(
            uploadConfigRows: uploadConfigRows,
            sourceUploadConfigRows: sourceUploadConfigRows,
            sourceNamesByUploadTarget: sourceNamesByUploadTarget,
            replacementHosterClassName: replacement.HosterClassName
        );
        var skippedUploadConfigTemplateIds = GetSkippedUploadConfigTemplateIds(
            uploadConfigTemplateRows: uploadConfigTemplateRows,
            sourceNamesByUploadTarget: sourceNamesByUploadTarget,
            replacementHosterClassName: replacement.HosterClassName
        );

        var skippedUploadConfigRows = sourceUploadConfigRows
            .Where(row => skipReasonsByUploadConfigId.ContainsKey(row.Id))
            .ToList();
        var skippedUploadConfigTemplateRows = sourceUploadConfigTemplateRows
            .Where(row => skippedUploadConfigTemplateIds.Contains(row.Id))
            .ToList();

        return new HosterReplacementPlan(
            SourceHosterClassName: sources.First().HosterClassName,
            Changes: CreateChanges(
                replacedUploadConfigRows: sourceUploadConfigRows
                    .Where(row => !skipReasonsByUploadConfigId.ContainsKey(row.Id))
                    .ToList(),
                replacedUploadConfigTemplateRows: sourceUploadConfigTemplateRows
                    .Where(row => !skippedUploadConfigTemplateIds.Contains(row.Id))
                    .ToList(),
                sourceNamesByUploadTarget: sourceNamesByUploadTarget,
                replacement: replacement,
                hosterRegistrationIdsToDeactivate: request.MarkSourceHosterAsDead
                    ? await repository.GetHosterRegistrationIdsOfUploadTargetsAsync(
                        request.SourceUploadTargets,
                        cancellationToken
                    )
                    : []
            ),
            SkippedUploadConfigs: skippedUploadConfigRows
                .Select(row => new SkippedUploadConfigReadModel(
                    UploadConfigId: row.Id,
                    ReleaseId: row.ReleaseId,
                    ReleaseName: row.ReleaseName,
                    UploadConfigName: row.Name,
                    SkipReason: skipReasonsByUploadConfigId[row.Id]
                ))
                .ToList(),
            SkippedUploadConfigTemplates: skippedUploadConfigTemplateRows
                .Select(row => new SkippedUploadConfigTemplateReadModel(
                    UploadConfigTemplateId: row.Id,
                    ReleaseTemplateId: row.ReleaseTemplateId,
                    ReleaseTemplateName: row.ReleaseTemplateName,
                    UploadConfigTemplateDisplayName: row.Name
                        ?? sourceNamesByUploadTarget[row.UploadTarget],
                    SkipReason: HosterReplacementSkipReason.DuplicateOnReplacementHoster
                ))
                .ToList(),
            SourcePoolsBecomingUnused: GetSourcePoolsWithoutSkippedRow(
                sources: sources,
                skippedUploadConfigRows: skippedUploadConfigRows,
                skippedUploadConfigTemplateRows: skippedUploadConfigTemplateRows
            )
        );
    }

    private async Task<HosterReplacementPreview> CreatePreviewAsync(
        HosterReplacementRequest request,
        HosterReplacementPlan plan,
        CancellationToken cancellationToken
    )
    {
        var replacedUploadConfigIds = plan.Changes.ReplacedUploadConfigIds;
        var hosterRegistrationIdsToDeactivate = plan.Changes.HosterRegistrationIdsToDeactivate;

        var hosterClassNamesWithDownload = hosterFactory
            .GetHostersByName()
            .Where(hoster => hoster.Value is IHosterWithDownload)
            .Select(hoster => hoster.Key)
            .ToList();

        return new HosterReplacementPreview(
            UploadConfigCount: replacedUploadConfigIds.Count,
            UploadConfigTemplateCount: plan.Changes.ReplacedUploadConfigTemplateIds.Count,
            UnstartedUploadCount: await repository.GetUnstartedUploadCountAsync(
                replacedUploadConfigIds,
                cancellationToken
            ),
            UploadCountStayingOnSourceHoster: await repository.GetUploadCountStayingOnSourceHosterAsync(
                replacedUploadConfigIds,
                cancellationToken
            ),
            UploadConfigCountWithoutArchiveSource: await repository.GetUploadConfigCountWithoutArchiveSourceAsync(
                uploadConfigIds: replacedUploadConfigIds,
                sourceHosterClassName: plan.SourceHosterClassName,
                hosterClassNamesWithDownload: hosterClassNamesWithDownload,
                cancellationToken: cancellationToken
            ),
            AccountCountToDeactivate: await repository.GetActiveHosterRegistrationCountAsync(
                hosterRegistrationIdsToDeactivate,
                cancellationToken
            ),
            UploadCountToMarkOffline: await repository.GetUploadCountToMarkOfflineAsync(
                hosterRegistrationIdsToDeactivate,
                cancellationToken
            ),
            SkippedUploadConfigs: plan.SkippedUploadConfigs,
            SkippedUploadConfigTemplates: plan.SkippedUploadConfigTemplates,
            SourceAccountsInUnselectedPools: await repository.GetSourceAccountsInUnselectedPoolsAsync(
                sourceUploadTargets: request.SourceUploadTargets,
                replacementUploadTarget: request.ReplacementUploadTarget,
                cancellationToken: cancellationToken
            ),
            SourcePoolsBecomingUnused: plan.SourcePoolsBecomingUnused
        );
    }

    private static void EnsureSourcesAreDistinctAndDifferFromReplacement(
        HosterReplacementRequest request
    )
    {
        var sources = request.SourceUploadTargets;

        if (sources.Count == 0)
        {
            throw new InvalidOperationException("A hoster replacement needs at least one source.");
        }

        if (sources.Distinct().Count() != sources.Count)
        {
            throw new InvalidOperationException(
                "The sources of a hoster replacement must be distinct."
            );
        }

        if (sources.Contains(request.ReplacementUploadTarget))
        {
            throw new InvalidOperationException(
                "The replacement of a hoster replacement must not be one of its sources."
            );
        }
    }

    private void EnsureReplacementCanReplaceSources(
        IReadOnlyList<HosterReplacementUploadTargetReadModel> sources,
        HosterReplacementUploadTargetReadModel replacement
    )
    {
        var sourceHosterClassNames = sources
            .Select(source => source.HosterClassName)
            .Distinct()
            .ToList();

        if (sourceHosterClassNames.Count != 1)
        {
            throw new InvalidOperationException(
                $"All sources of a hoster replacement must belong to one hoster, but found {string.Join(", ", sourceHosterClassNames)}."
            );
        }

        if (!replacement.IsUsableForUploads)
        {
            throw new InvalidOperationException(
                $"Upload target {replacement.Name} is not usable for uploads."
            );
        }

        var sourceHoster = hosterFactory.GetByName(sourceHosterClassNames.First());
        var replacementHoster = hosterFactory.GetByName(replacement.HosterClassName);

        if (sourceHoster is IVideoHoster != replacementHoster is IVideoHoster)
        {
            throw new InvalidOperationException(
                $"Hoster {sourceHoster.Name} cannot be replaced by {replacementHoster.Name} because only one of them is a video hoster."
            );
        }
    }

    private static void EnsureDeadSourceHosterDiffersFromReplacementHoster(
        HosterReplacementRequest request,
        IReadOnlyList<HosterReplacementUploadTargetReadModel> sources,
        HosterReplacementUploadTargetReadModel replacement
    )
    {
        if (
            request.MarkSourceHosterAsDead
            && sources.First().HosterClassName == replacement.HosterClassName
        )
        {
            throw new InvalidOperationException(
                $"Hoster {replacement.HosterClassName} cannot be marked as dead because the replacement uses the same hoster."
            );
        }
    }

    private static Dictionary<int, HosterReplacementSkipReason> GetSkipReasonsByUploadConfigId(
        IReadOnlyList<HosterReplacementUploadConfigRow> uploadConfigRows,
        IReadOnlyList<HosterReplacementUploadConfigRow> sourceUploadConfigRows,
        IReadOnlyDictionary<UploadTarget, string> sourceNamesByUploadTarget,
        string replacementHosterClassName
    )
    {
        var duplicateUploadConfigIds = GetIdsOfSourceRowsToSkipAsDuplicates(
            uploadConfigRows
                .Select(row => new RowInConflictGroup(
                    Id: row.Id,
                    ConflictGroup: (row.ReleaseId, row.UploadSource, row.ArchiveConfigId),
                    IsSourceRow: sourceNamesByUploadTarget.ContainsKey(row.UploadTarget),
                    HosterClassName: row.HosterClassName
                ))
                .ToList(),
            replacementHosterClassName
        );

        var partiallySkippedCollectionUploadSlotIds = sourceUploadConfigRows
            .Where(row =>
                row.CollectionUploadSlotId is not null && duplicateUploadConfigIds.Contains(row.Id)
            )
            .Select(row => row.CollectionUploadSlotId)
            .ToHashSet();

        var skipReasonsByUploadConfigId = new Dictionary<int, HosterReplacementSkipReason>();

        foreach (var row in sourceUploadConfigRows)
        {
            if (duplicateUploadConfigIds.Contains(row.Id))
            {
                skipReasonsByUploadConfigId[row.Id] =
                    HosterReplacementSkipReason.DuplicateOnReplacementHoster;
            }
            else if (partiallySkippedCollectionUploadSlotIds.Contains(row.CollectionUploadSlotId))
            {
                skipReasonsByUploadConfigId[row.Id] =
                    HosterReplacementSkipReason.CollectionUploadSlotPartiallySkipped;
            }
        }

        return skipReasonsByUploadConfigId;
    }

    private static HashSet<int> GetSkippedUploadConfigTemplateIds(
        IReadOnlyList<HosterReplacementUploadConfigTemplateRow> uploadConfigTemplateRows,
        IReadOnlyDictionary<UploadTarget, string> sourceNamesByUploadTarget,
        string replacementHosterClassName
    )
    {
        return GetIdsOfSourceRowsToSkipAsDuplicates(
            uploadConfigTemplateRows
                .Select(row => new RowInConflictGroup(
                    Id: row.Id,
                    ConflictGroup: (
                        row.ReleaseTemplateId,
                        row.UploadSource,
                        row.ArchiveConfigTemplateId
                    ),
                    IsSourceRow: sourceNamesByUploadTarget.ContainsKey(row.UploadTarget),
                    HosterClassName: row.HosterClassName
                ))
                .ToList(),
            replacementHosterClassName
        );
    }

    private static HashSet<int> GetIdsOfSourceRowsToSkipAsDuplicates(
        IReadOnlyList<RowInConflictGroup> rows,
        string replacementHosterClassName
    )
    {
        var skippedIds = new HashSet<int>();

        foreach (var rowsInConflictGroup in rows.GroupBy(row => row.ConflictGroup))
        {
            var sourceRows = rowsInConflictGroup
                .Where(row => row.IsSourceRow)
                .OrderBy(row => row.Id)
                .ToList();
            var groupIsAlreadyOnReplacementHoster = rowsInConflictGroup.Any(row =>
                !row.IsSourceRow && row.HosterClassName == replacementHosterClassName
            );
            var rowsToSkip = groupIsAlreadyOnReplacementHoster ? sourceRows : sourceRows.Skip(1);

            skippedIds.UnionWith(rowsToSkip.Select(row => row.Id));
        }

        return skippedIds;
    }

    private HosterReplacementChanges CreateChanges(
        IReadOnlyList<HosterReplacementUploadConfigRow> replacedUploadConfigRows,
        IReadOnlyList<HosterReplacementUploadConfigTemplateRow> replacedUploadConfigTemplateRows,
        IReadOnlyDictionary<UploadTarget, string> sourceNamesByUploadTarget,
        HosterReplacementUploadTargetReadModel replacement,
        IReadOnlyList<int> hosterRegistrationIdsToDeactivate
    )
    {
        var replacementHosterSupportsPremiumOnlyDownloads = hosterFactory
            .GetByName(replacement.HosterClassName)
            .SupportsPremiumOnlyDownloads;

        return new HosterReplacementChanges(
            ReplacementUploadTarget: replacement.UploadTarget,
            ReplacementUploadTargetName: replacement.Name,
            ReplacementHosterSupportsPremiumOnlyDownloads: replacementHosterSupportsPremiumOnlyDownloads,
            ReplacedUploadConfigIds: replacedUploadConfigRows.Select(row => row.Id).ToList(),
            RenamedUploadConfigIds: replacedUploadConfigRows
                .Where(row => row.Name == sourceNamesByUploadTarget[row.UploadTarget])
                .Select(row => row.Id)
                .ToList(),
            UploadConfigIdsToResetPremiumOnlyDownload: replacementHosterSupportsPremiumOnlyDownloads
                ? []
                : replacedUploadConfigRows
                    .Where(row => row.PremiumOnlyDownload)
                    .Select(row => row.Id)
                    .ToList(),
            ReplacedUploadConfigTemplateIds: replacedUploadConfigTemplateRows
                .Select(row => row.Id)
                .ToList(),
            RenamedUploadConfigTemplateIds: replacedUploadConfigTemplateRows
                .Where(row => row.Name == sourceNamesByUploadTarget[row.UploadTarget])
                .Select(row => row.Id)
                .ToList(),
            UploadConfigTemplateIdsToResetPremiumOnlyDownload: replacementHosterSupportsPremiumOnlyDownloads
                ? []
                : replacedUploadConfigTemplateRows
                    .Where(row => row.PremiumOnlyDownload)
                    .Select(row => row.Id)
                    .ToList(),
            HosterRegistrationIdsToDeactivate: hosterRegistrationIdsToDeactivate,
            LocalNow: timeProvider.GetLocalNow()
        );
    }

    private static List<SourcePoolBecomingUnusedReadModel> GetSourcePoolsWithoutSkippedRow(
        IReadOnlyList<HosterReplacementUploadTargetReadModel> sources,
        IReadOnlyList<HosterReplacementUploadConfigRow> skippedUploadConfigRows,
        IReadOnlyList<HosterReplacementUploadConfigTemplateRow> skippedUploadConfigTemplateRows
    )
    {
        var poolIdsWithSkippedRow = skippedUploadConfigRows
            .Select(row => row.HosterAccountPoolId)
            .Concat(skippedUploadConfigTemplateRows.Select(row => row.HosterAccountPoolId))
            .ToHashSet();

        return sources
            .Where(source =>
                source.UploadTarget.HosterAccountPoolId is not null
                && !poolIdsWithSkippedRow.Contains(source.UploadTarget.HosterAccountPoolId)
            )
            .Select(source => new SourcePoolBecomingUnusedReadModel(
                source.UploadTarget.HosterAccountPoolId!.Value,
                source.Name
            ))
            .ToList();
    }

    private sealed record RowInConflictGroup(
        int Id,
        (
            int ReleaseOrTemplateId,
            UploadSource UploadSource,
            int? ArchiveConfigOrTemplateId
        ) ConflictGroup,
        bool IsSourceRow,
        string HosterClassName
    );

    private sealed record HosterReplacementPlan(
        string SourceHosterClassName,
        HosterReplacementChanges Changes,
        IReadOnlyList<SkippedUploadConfigReadModel> SkippedUploadConfigs,
        IReadOnlyList<SkippedUploadConfigTemplateReadModel> SkippedUploadConfigTemplates,
        IReadOnlyList<SourcePoolBecomingUnusedReadModel> SourcePoolsBecomingUnused
    );
}
