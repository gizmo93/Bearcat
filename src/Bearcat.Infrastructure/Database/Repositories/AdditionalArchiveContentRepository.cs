using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.ReadModels;
using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class AdditionalArchiveContentRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite
) : IAdditionalArchiveContentReadRepository, IAdditionalArchiveContentWriteRepository
{
    public async Task<IReadOnlyList<AdditionalArchiveContentReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .AdditionalArchiveContents.OrderBy(content => content.Name)
            .ThenBy(content => content.Id)
            .Select(content => new AdditionalArchiveContentReadModel(
                content.Id,
                content.Name,
                content.Type,
                content.SourcePath,
                content.FileName,
                dbRead.ArchiveConfigTemplates.Count(template =>
                    template.AdditionalArchiveContents.Any(assigned => assigned.Id == content.Id)
                ),
                dbRead.ArchiveConfigs.Count(archiveConfig =>
                    archiveConfig.AdditionalArchiveContents.Any(assigned =>
                        assigned.Id == content.Id
                    )
                )
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdditionalArchiveContentDetailReadModel?> GetDetailReadModelAsync(
        int additionalArchiveContentId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .AdditionalArchiveContents.Where(content => content.Id == additionalArchiveContentId)
            .Select(content => new AdditionalArchiveContentDetailReadModel(
                content.Id,
                content.Name,
                content.Type,
                content.SourcePath,
                content.FileName,
                content.TextContent
            ))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AdditionalArchiveContent> GetByIdAsync(
        int additionalArchiveContentId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite.AdditionalArchiveContents.FirstAsync(
            content => content.Id == additionalArchiveContentId,
            cancellationToken
        );
    }

    public async Task<bool> NameExistsAsync(
        string name,
        int? excludedAdditionalArchiveContentId,
        CancellationToken cancellationToken
    )
    {
        return await dbRead.AdditionalArchiveContents.AnyAsync(
            content => content.Name == name && content.Id != excludedAdditionalArchiveContentId,
            cancellationToken
        );
    }

    public async Task<AdditionalArchiveContentUsageReadModel> GetUsageAsync(
        int additionalArchiveContentId,
        CancellationToken cancellationToken
    )
    {
        var releaseTemplateNames = await dbRead
            .ArchiveConfigTemplates.Where(template =>
                template.AdditionalArchiveContents.Any(content =>
                    content.Id == additionalArchiveContentId
                )
            )
            .Select(template => template.ReleaseTemplate.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);

        var releaseNames = await dbRead
            .ArchiveConfigs.Where(archiveConfig =>
                archiveConfig.AdditionalArchiveContents.Any(content =>
                    content.Id == additionalArchiveContentId
                )
            )
            .Select(archiveConfig => archiveConfig.Release.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);

        return new AdditionalArchiveContentUsageReadModel(releaseTemplateNames, releaseNames);
    }

    public void Add(AdditionalArchiveContent additionalArchiveContent)
    {
        dbWrite.Add(additionalArchiveContent);
    }

    public void Remove(AdditionalArchiveContent additionalArchiveContent)
    {
        dbWrite.Remove(additionalArchiveContent);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
