using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;

namespace Bearcat.Domain.UseCases.ManageForumPostTemplates.Repositories;

public interface IForumPostTemplateReadRepository
{
    Task<IReadOnlyList<ForumPostTemplateSummaryReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    );

    Task<ForumPostTemplateDetailReadModel?> GetDetailAsync(
        int forumPostTemplateId,
        CancellationToken cancellationToken = default
    );
}
