using Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;

public interface IReleaseTemplateReadRepository
{
    Task<IReadOnlyList<ReleaseTemplateSummaryReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    );

    Task<ReleaseTemplateDetailReadModel?> GetDetailAsync(
        int releaseTemplateId,
        CancellationToken cancellationToken = default
    );
}
