using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageForumPostTemplates.Repositories;

public interface IForumPostTemplatePreviewEntityReadRepository
{
    Task<IReadOnlyList<ForumPostTemplatePreviewEntityReadModel>> SearchAsync(
        ForumPostTemplateType type,
        string? searchTerm,
        int limit,
        CancellationToken cancellationToken = default
    );

    Task<ForumPostTemplatePreviewEntityReadModel?> GetAsync(
        ForumPostTemplateType type,
        int entityId,
        CancellationToken cancellationToken = default
    );
}
