using Bearcat.Domain.UseCases.ManageReleaseTemplates.Dto;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;

public interface IReleaseTemplateReadRepository
{
    Task<IReadOnlyList<ReleaseTemplateSummaryDto>> GetAllAsync(
        CancellationToken cancellationToken = default
    );

    Task<ReleaseTemplateDetailDto?> GetDetailAsync(
        int releaseTemplateId,
        CancellationToken cancellationToken = default
    );
}
