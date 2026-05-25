using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ForumPostTemplateRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite
) : IForumPostTemplateReadRepository, IForumPostTemplateWriteRepository
{
    public async Task<IReadOnlyList<ForumPostTemplateSummaryReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ForumPostTemplates.OrderBy(template => template.Name)
            .ThenBy(template => template.Id)
            .Select(template => new ForumPostTemplateSummaryReadModel(
                template.Id,
                template.Name,
                template.UpdatedAt
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<ForumPostTemplateDetailReadModel?> GetDetailAsync(
        int forumPostTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ForumPostTemplates.Where(template => template.Id == forumPostTemplateId)
            .Select(template => new ForumPostTemplateDetailReadModel(
                template.Id,
                template.Name,
                template.TemplateBody
            ))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public void Add(ForumPostTemplate template)
    {
        dbWrite.Add(template);
    }

    public void Remove(ForumPostTemplate template)
    {
        dbWrite.Remove(template);
    }

    public async Task<ForumPostTemplate> GetByIdAsync(
        int forumPostTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite.ForumPostTemplates.FirstAsync(
            template => template.Id == forumPostTemplateId,
            cancellationToken
        );
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
