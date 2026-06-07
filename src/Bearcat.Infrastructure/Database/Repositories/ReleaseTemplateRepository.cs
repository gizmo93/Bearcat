using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ReleaseTemplateRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite,
    IArchiverFactory archiverFactory,
    ILinkCrypterFactory linkCrypterFactory
) : IReleaseTemplateReadRepository, IReleaseTemplateWriteRepository
{
    public async Task<IReadOnlyList<ReleaseTemplateSummaryReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ReleaseTemplates.OrderBy(t => t.Name)
            .ThenBy(t => t.Id)
            .Select(t => new ReleaseTemplateSummaryReadModel(
                t.Id,
                t.Name,
                t.ReleaseType,
                t.ReleaseGroupId,
                t.ReleaseGroup.Name,
                t.UseReleaseCollections,
                t.ReleaseCollectionDetectionMode,
                t.IgnoreLanguageInReleaseCollectionName,
                t.ReleaseCollectionPattern,
                t.ReleaseCollectionKeyTemplate,
                t.ReleaseCollectionNameTemplate,
                t.ArchiveConfigTemplates.Count,
                t.UploadConfigTemplates.Count,
                t.ImageUploadConfigTemplates.Count,
                t.UploadConfigTemplates.SelectMany(u => u.LinkCrypterTemplates).Count()
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<ReleaseTemplateDetailReadModel?> GetDetailAsync(
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
            .Include(t => t.ImageUploadConfigTemplates)
                .ThenInclude(i => i.ImageHosterRegistration)
            .FirstOrDefaultAsync(t => t.Id == releaseTemplateId, cancellationToken);

        return releaseTemplate is null ? null : ToDetailReadModel(releaseTemplate);
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

    public void Remove(ImageUploadConfigTemplate imageUploadConfigTemplate)
    {
        dbWrite.Remove(imageUploadConfigTemplate);
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
            .Include(t => t.ImageUploadConfigTemplates)
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
            .Include(r => r.ImageUploadConfigs)
                .ThenInclude(i => i.ImageHosterRegistration)
            .FirstAsync(r => r.Id == releaseId, cancellationToken);
    }

    public async Task<ArchiveConfigTemplate> GetArchiveConfigTemplateAsync(
        int archiveConfigTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .ArchiveConfigTemplates.Include(a => a.UploadConfigTemplates)
            .Include(a => a.ReleaseTemplate)
            .FirstAsync(a => a.Id == archiveConfigTemplateId, cancellationToken);
    }

    public async Task<UploadConfigTemplate> GetUploadConfigTemplateAsync(
        int uploadConfigTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .UploadConfigTemplates.Include(u => u.LinkCrypterTemplates)
            .Include(u => u.ReleaseTemplate)
                .ThenInclude(t => t.ArchiveConfigTemplates)
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

    public async Task<ImageUploadConfigTemplate> GetImageUploadConfigTemplateAsync(
        int imageUploadConfigTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite.ImageUploadConfigTemplates.FirstAsync(
            template => template.Id == imageUploadConfigTemplateId,
            cancellationToken
        );
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }

    private ReleaseTemplateDetailReadModel ToDetailReadModel(ReleaseTemplate releaseTemplate)
    {
        var archiverNames = archiverFactory
            .GetArchivers()
            .ToDictionary(archiver => archiver.ClassName, archiver => archiver.Name);
        var linkCryptersByClassName = linkCrypterFactory
            .GetLinkCrypters()
            .ToDictionary(linkCrypter => linkCrypter.ClassName);

        return new ReleaseTemplateDetailReadModel(
            releaseTemplate.Id,
            releaseTemplate.Name,
            releaseTemplate.ReleaseType,
            releaseTemplate.ReleaseGroupId,
            releaseTemplate.ReleaseGroup.Name,
            releaseTemplate.UseReleaseCollections,
            releaseTemplate.ReleaseCollectionDetectionMode,
            releaseTemplate.IgnoreLanguageInReleaseCollectionName,
            releaseTemplate.ReleaseCollectionPattern,
            releaseTemplate.ReleaseCollectionKeyTemplate,
            releaseTemplate.ReleaseCollectionNameTemplate,
            releaseTemplate
                .ArchiveConfigTemplates.OrderBy(a => a.Name)
                .ThenBy(a => a.Id)
                .Select(a => new ArchiveConfigTemplateReadModel(
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
                .Select(u => new UploadConfigTemplateReadModel(
                    u.Id,
                    u.Name,
                    string.IsNullOrWhiteSpace(u.Name) ? u.HosterRegistration.Name : u.Name,
                    u.HosterRegistrationId,
                    u.HosterRegistration.Name,
                    u.ArchiveConfigTemplateId,
                    releaseTemplate
                        .ArchiveConfigTemplates.First(a => a.Id == u.ArchiveConfigTemplateId)
                        .Name,
                    u.PremiumOnlyDownload,
                    u.CollectionUploadSlotKey,
                    u.CollectionUploadSlotName,
                    u.CollectionUploadSlotIsRequired,
                    u.CollectionUploadSlotPasswordPolicy,
                    u.CollectionUploadSlotExpectedArchivePassword,
                    u.LinksDistributedTo,
                    u.LinkCrypterTemplates.OrderBy(l => l.LinkCrypterRegistration.Name)
                        .ThenBy(l => l.Id)
                        .Select(l => new UploadConfigLinkCrypterTemplateReadModel(
                            l.Id,
                            l.LinkCrypterRegistrationId,
                            l.LinkCrypterRegistration.Name,
                            linkCryptersByClassName[
                                l.LinkCrypterRegistration.LinkCrypterClassName
                            ].Name,
                            l.ContainerScope,
                            l.Password,
                            l.EnableCaptcha,
                            l.EnableContainerDownload,
                            l.EnableClickAndLoad,
                            linkCryptersByClassName[
                                l.LinkCrypterRegistration.LinkCrypterClassName
                            ].SupportsCaptcha,
                            linkCryptersByClassName[
                                l.LinkCrypterRegistration.LinkCrypterClassName
                            ].SupportsContainerDownload,
                            linkCryptersByClassName[
                                l.LinkCrypterRegistration.LinkCrypterClassName
                            ].SupportsClickAndLoad
                        ))
                        .ToList()
                ))
                .ToList(),
            releaseTemplate
                .ImageUploadConfigTemplates.OrderBy(i => i.Name ?? i.ImageHosterRegistration.Name)
                .ThenBy(i => i.Id)
                .Select(i => new ImageUploadConfigTemplateReadModel(
                    i.Id,
                    i.Name,
                    string.IsNullOrWhiteSpace(i.Name) ? i.ImageHosterRegistration.Name : i.Name,
                    i.ImageHosterRegistrationId,
                    i.ImageHosterRegistration.Name
                ))
                .ToList()
        );
    }
}
