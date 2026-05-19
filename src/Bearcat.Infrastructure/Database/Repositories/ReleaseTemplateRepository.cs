using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Dto;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ReleaseTemplateRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite,
    IArchiverFactory archiverFactory
) : IReleaseTemplateReadRepository, IReleaseTemplateWriteRepository
{
    public async Task<IReadOnlyList<ReleaseTemplateSummaryDto>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ReleaseTemplates.OrderBy(t => t.Name)
            .ThenBy(t => t.Id)
            .Select(t => new ReleaseTemplateSummaryDto(
                t.Id,
                t.Name,
                t.ReleaseType,
                t.ReleaseGroupId,
                t.ReleaseGroup.Name,
                t.ArchiveConfigTemplates.Count(),
                t.UploadConfigTemplates.Count(),
                t.UploadConfigTemplates.SelectMany(u => u.LinkCrypterTemplates).Count()
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<ReleaseTemplateDetailDto?> GetDetailAsync(
        int releaseTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        var releaseTemplate = await dbRead
            .ReleaseTemplates.AsSplitQuery()
            .Include(t => t.ReleaseGroup)
            .Include(t => t.ArchiveConfigTemplates)
            .Include(t => t.UploadConfigTemplates)
                .ThenInclude(u => u.HosterRegistration)
            .Include(t => t.UploadConfigTemplates)
                .ThenInclude(u => u.LinkCrypterTemplates)
                    .ThenInclude(l => l.LinkCrypterRegistration)
            .FirstOrDefaultAsync(t => t.Id == releaseTemplateId, cancellationToken);

        return releaseTemplate is null ? null : ToDetailDto(releaseTemplate);
    }

    public void Add(ReleaseTemplate releaseTemplate)
    {
        dbWrite.Add(releaseTemplate);
    }

    public void Remove(ReleaseTemplate releaseTemplate)
    {
        dbWrite.Remove(releaseTemplate);
    }

    public void Remove(ArchiveConfigTemplate archiveConfigTemplate)
    {
        dbWrite.Remove(archiveConfigTemplate);
    }

    public void Remove(UploadConfigTemplate uploadConfigTemplate)
    {
        dbWrite.Remove(uploadConfigTemplate);
    }

    public void Remove(UploadConfigLinkCrypterTemplate linkCrypterTemplate)
    {
        dbWrite.Remove(linkCrypterTemplate);
    }

    public async Task<ReleaseTemplate> GetByIdAsync(
        int releaseTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .ReleaseTemplates.Include(t => t.ReleaseGroup)
            .FirstAsync(t => t.Id == releaseTemplateId, cancellationToken);
    }

    public async Task<ReleaseTemplate> GetByIdWithChildrenAsync(
        int releaseTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .ReleaseTemplates.AsSplitQuery()
            .Include(t => t.ReleaseGroup)
            .Include(t => t.ArchiveConfigTemplates)
            .Include(t => t.UploadConfigTemplates)
                .ThenInclude(u => u.LinkCrypterTemplates)
            .FirstAsync(t => t.Id == releaseTemplateId, cancellationToken);
    }

    public async Task<Release> GetReleaseForTemplateCreationAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .Releases.AsSplitQuery()
            .Include(r => r.ArchiveConfigs)
            .Include(r => r.UploadConfigs)
                .ThenInclude(u => u.HosterRegistration)
            .Include(r => r.UploadConfigs)
                .ThenInclude(u => u.LinkCrypters)
            .FirstAsync(r => r.Id == releaseId, cancellationToken);
    }

    public async Task<ArchiveConfigTemplate> GetArchiveConfigTemplateAsync(
        int archiveConfigTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .ArchiveConfigTemplates.Include(a => a.UploadConfigTemplates)
            .FirstAsync(a => a.Id == archiveConfigTemplateId, cancellationToken);
    }

    public async Task<UploadConfigTemplate> GetUploadConfigTemplateAsync(
        int uploadConfigTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .UploadConfigTemplates.Include(u => u.LinkCrypterTemplates)
            .FirstAsync(u => u.Id == uploadConfigTemplateId, cancellationToken);
    }

    public async Task<UploadConfigLinkCrypterTemplate> GetUploadConfigLinkCrypterTemplateAsync(
        int uploadConfigLinkCrypterTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite.UploadConfigLinkCrypterTemplates.FirstAsync(
            l => l.Id == uploadConfigLinkCrypterTemplateId,
            cancellationToken
        );
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }

    private ReleaseTemplateDetailDto ToDetailDto(ReleaseTemplate releaseTemplate)
    {
        var archiverNames = archiverFactory
            .GetArchivers()
            .ToDictionary(archiver => archiver.ClassName, archiver => archiver.Name);

        return new ReleaseTemplateDetailDto(
            releaseTemplate.Id,
            releaseTemplate.Name,
            releaseTemplate.ReleaseType,
            releaseTemplate.ReleaseGroupId,
            releaseTemplate.ReleaseGroup.Name,
            releaseTemplate
                .ArchiveConfigTemplates.OrderBy(a => a.Name)
                .ThenBy(a => a.Id)
                .Select(a => new ArchiveConfigTemplateDto(
                    a.Id,
                    a.Name,
                    a.ArchiveFilesBasePath,
                    a.ArchiverName,
                    archiverNames.GetValueOrDefault(a.ArchiverName, a.ArchiverName),
                    a.ArchivePassword,
                    a.ArchiveFileSizeMb,
                    a.UseReleaseNameAsArchiveName,
                    a.UploadConfigTemplates.Count
                ))
                .ToList(),
            releaseTemplate
                .UploadConfigTemplates.OrderBy(u => u.Name ?? u.HosterRegistration.Name)
                .ThenBy(u => u.Id)
                .Select(u => new UploadConfigTemplateDto(
                    u.Id,
                    u.Name,
                    string.IsNullOrWhiteSpace(u.Name) ? u.HosterRegistration.Name : u.Name,
                    u.HosterRegistrationId,
                    u.HosterRegistration.Name,
                    u.ArchiveConfigTemplateId,
                    releaseTemplate
                        .ArchiveConfigTemplates.First(a => a.Id == u.ArchiveConfigTemplateId)
                        .Name,
                    u.LinksDistributedTo,
                    u.LinkCrypterTemplates.OrderBy(l => l.LinkCrypterRegistration.Name)
                        .ThenBy(l => l.Id)
                        .Select(l => new UploadConfigLinkCrypterTemplateDto(
                            l.Id,
                            l.LinkCrypterRegistrationId,
                            l.LinkCrypterRegistration.Name,
                            l.LinkCrypterRegistration.LinkCrypterClassName,
                            l.Password
                        ))
                        .ToList()
                ))
                .ToList()
        );
    }
}
