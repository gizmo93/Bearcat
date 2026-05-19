using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.Dto;
using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.Repositories;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ReleaseFolderAutomationRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite
) : IReleaseFolderAutomationReadRepository,
    IReleaseFolderAutomationWriteRepository,
    IAutomaticallyCreateReleasesRepository
{
    public async Task<IReadOnlyList<ReleaseFolderAutomationDto>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ReleaseFolderAutomations.OrderBy(a => a.BasePath)
            .ThenBy(a => a.FolderNamePattern)
            .ThenBy(a => a.Id)
            .Select(a => new ReleaseFolderAutomationDto(
                a.Id,
                a.BasePath,
                a.FolderNamePattern,
                a.ReleaseTemplateId,
                a.ReleaseTemplate.Name,
                a.IsEnabled
            ))
            .ToListAsync(cancellationToken);
    }

    public void Add(ReleaseFolderAutomation automation)
    {
        dbWrite.Add(automation);
    }

    public void Add(Release release)
    {
        dbWrite.Add(release);
    }

    public void Add(Notification notification)
    {
        dbWrite.Add(notification);
    }

    public void Remove(ReleaseFolderAutomation automation)
    {
        dbWrite.Remove(automation);
    }

    public async Task<ReleaseFolderAutomation> GetByIdAsync(
        int releaseFolderAutomationId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite.ReleaseFolderAutomations.FirstAsync(
            automation => automation.Id == releaseFolderAutomationId,
            cancellationToken
        );
    }

    public async Task<IReadOnlyList<ReleaseFolderAutomation>> GetEnabledWithTemplatesAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .ReleaseFolderAutomations.AsSplitQuery()
            .Where(automation => automation.IsEnabled)
            .Include(automation => automation.ReleaseTemplate)
            .ThenInclude(template => template.ArchiveConfigTemplates)
            .Include(automation => automation.ReleaseTemplate)
            .ThenInclude(template => template.UploadConfigTemplates)
            .ThenInclude(uploadTemplate => uploadTemplate.HosterRegistration)
            .Include(automation => automation.ReleaseTemplate)
            .ThenInclude(template => template.UploadConfigTemplates)
            .ThenInclude(uploadTemplate => uploadTemplate.LinkCrypterTemplates)
            .ToListAsync(cancellationToken);
    }

    public async Task<HashSet<string>> GetExistingReleaseFolderPathsAsync(
        IReadOnlyCollection<string> releaseFolderPaths,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .Releases.Where(release => releaseFolderPaths.Contains(release.ReleaseFolderPath))
            .Select(release => release.ReleaseFolderPath)
            .ToHashSetAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
