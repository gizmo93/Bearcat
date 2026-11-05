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

    Task<
        IReadOnlyList<SourceAccountInUnselectedPoolReadModel>
    > GetSourceAccountsInUnselectedPoolsAsync(
        IReadOnlyList<UploadTarget> sourceUploadTargets,
        CancellationToken cancellationToken
    );

    Task<int> ApplyChangesAndGetChangedUploadCountAsync(
        HosterReplacementChanges changes,
        CancellationToken cancellationToken
    );
}
