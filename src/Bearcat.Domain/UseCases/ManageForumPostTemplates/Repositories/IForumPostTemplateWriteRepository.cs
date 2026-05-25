using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageForumPostTemplates.Repositories;

public interface IForumPostTemplateWriteRepository
{
    void Add(ForumPostTemplate template);

    void Remove(ForumPostTemplate template);

    Task<ForumPostTemplate> GetByIdAsync(
        int forumPostTemplateId,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
