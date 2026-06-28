using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.Repositories;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ReleaseFolderAutomationRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite
)
    : IReleaseFolderAutomationReadRepository,
        IReleaseFolderAutomationWriteRepository,
        IAutomaticallyCreateReleasesRepository
{
    public async Task<IReadOnlyList<ReleaseFolderAutomationReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ReleaseFolderAutomations.OrderBy(a => a.BasePath)
            .ThenBy(a => a.FolderNamePattern)
            .ThenBy(a => a.Id)
            .Select(a => new ReleaseFolderAutomationReadModel(
                a.Id,
                a.BasePath,
                a.FolderNamePattern,
                a.ReleaseTemplateId,
                a.ReleaseTemplate.Name,
                a.ReleaseTemplate.ReleaseType,
                a.ReleaseTemplate.ReleaseContentType,
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
            .Include(automation => automation.ReleaseTemplate)
                .ThenInclude(template => template.ImageUploadConfigTemplates)
                    .ThenInclude(imageTemplate => imageTemplate.ImageHosterRegistration)
            .Include(automation => automation.ReleaseTemplate)
                .ThenInclude(template => template.CollectionImageUploadConfigTemplates)
                    .ThenInclude(imageTemplate => imageTemplate.ImageHosterRegistration)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReleaseFolderObservation>> GetFolderObservationsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite.ReleaseFolderObservations.ToListAsync(cancellationToken);
    }

    public void AddFolderObservation(ReleaseFolderObservation observation)
    {
        dbWrite.Add(observation);
    }

    public void RemoveFolderObservation(ReleaseFolderObservation observation)
    {
        dbWrite.Remove(observation);
    }

    public async Task<HashSet<string>> GetExistingReleaseFolderPathsAsync(
        IReadOnlyCollection<string> releaseFolderPaths,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .Releases.Where(release =>
                release.ReleaseFolderPath != null
                && releaseFolderPaths.Contains(release.ReleaseFolderPath)
            )
            .Select(release => release.ReleaseFolderPath!)
            .ToHashSetAsync(cancellationToken);
    }

    public async Task<HashSet<string>> GetExistingArchiveFolderPathsAsync(
        IReadOnlyCollection<string> archiveFolderPaths,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .Releases.Where(release => release.ReleaseType == ReleaseType.Unmanaged)
            .SelectMany(release => release.ArchiveConfigs)
            .Select(archiveConfig => archiveConfig.ArchiveFilesBasePath)
            .Where(archiveFolderPath => archiveFolderPaths.Contains(archiveFolderPath))
            .ToHashSetAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
