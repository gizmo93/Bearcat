using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;
using Bearcat.Domain.UseCases.ReplaceHosters.Dto;
using Bearcat.Domain.UseCases.ReplaceHosters.ReadModels;

namespace Bearcat.Domain.UseCases.ReplaceHosters.Repositories;

public interface IHosterReplacementRepository
{
    Task<IReadOnlyList<HosterReplacementUploadTargetReadModel>> GetUploadTargetsAsync(
        IReadOnlyList<UploadTarget> uploadTargets,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<HosterReplacementSourceChoiceReadModel>> GetSourceChoicesAsync(
        string hosterClassName,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<int>> GetHosterRegistrationIdsOfUploadTargetsAsync(
        IReadOnlyList<UploadTarget> uploadTargets,
        CancellationToken cancellationToken
    );

    Task<
        IReadOnlyList<HosterReplacementUploadConfigRow>
    > GetUploadConfigRowsOfReleasesContainingUploadTargetsAsync(
        IReadOnlyList<UploadTarget> uploadTargets,
        CancellationToken cancellationToken
    );

    Task<
        IReadOnlyList<HosterReplacementUploadConfigTemplateRow>
    > GetUploadConfigTemplateRowsOfReleaseTemplatesContainingUploadTargetsAsync(
        IReadOnlyList<UploadTarget> uploadTargets,
        CancellationToken cancellationToken
    );

    Task<int> GetUnstartedUploadCountAsync(
        IReadOnlyList<int> uploadConfigIds,
        CancellationToken cancellationToken
    );

    Task<int> GetUploadCountStayingOnSourceHosterAsync(
        IReadOnlyList<int> uploadConfigIds,
        CancellationToken cancellationToken
    );

    Task<int> GetUploadConfigCountWithoutArchiveSourceAsync(
        IReadOnlyList<int> uploadConfigIds,
        string sourceHosterClassName,
        IReadOnlyList<string> hosterClassNamesWithDownload,
        CancellationToken cancellationToken
    );

    Task<int> GetActiveHosterRegistrationCountAsync(
        IReadOnlyList<int> hosterRegistrationIds,
        CancellationToken cancellationToken
    );

    Task<int> GetUploadCountToMarkOfflineAsync(
        IReadOnlyList<int> hosterRegistrationIds,
        IReadOnlyList<int> uploadConfigIdsToDelete,
        CancellationToken cancellationToken
    );

    Task<
        IReadOnlyList<SourceAccountInUnselectedPoolReadModel>
    > GetSourceAccountsInUnselectedPoolsAsync(
        IReadOnlyList<UploadTarget> sourceUploadTargets,
        UploadTarget replacementUploadTarget,
        CancellationToken cancellationToken
    );

    Task<HosterReplacementAppliedChangeCounts> ApplyChangesAndGetChangeCountsAsync(
        HosterReplacementChanges changes,
        CancellationToken cancellationToken
    );
}
