using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageForumPostTemplates.Repositories;

public interface IForumPostTemplateReadRepository
{
    Task<IReadOnlyList<ForumPostTemplateSummaryReadModel>> GetAllAsync(
        ForumPostTemplateType? type = null,
        ForumPostTemplateOutputFormat? outputFormat = null,
        CancellationToken cancellationToken = default
    );

    Task<ForumPostTemplateDetailReadModel?> GetDetailAsync(
        int forumPostTemplateId,
        CancellationToken cancellationToken = default
    );
}
