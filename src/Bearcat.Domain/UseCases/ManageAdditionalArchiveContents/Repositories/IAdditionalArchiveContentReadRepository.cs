using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.ReadModels;

namespace Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Repositories;

public interface IAdditionalArchiveContentReadRepository
{
    Task<IReadOnlyList<AdditionalArchiveContentReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    );

    Task<AdditionalArchiveContentDetailReadModel?> GetDetailReadModelAsync(
        int additionalArchiveContentId,
        CancellationToken cancellationToken = default
    );
}
