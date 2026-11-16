using Bearcat.Domain.UseCases.ManageArchiveConfigs.PartSizeChange.ReadModels;
using Bearcat.Domain.UseCases.ManageArchiveConfigs.PartSizeChange.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ArchivePartSizeChangeRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite
) : IArchivePartSizeChangeRepository
{
    public async Task<IReadOnlyList<ArchivePartSizeChoiceReadModel>> GetPartSizeChoicesAsync(
        CancellationToken cancellationToken
    )
    {
        var archiveConfigCountsBySize = await dbRead
            .ArchiveConfigs.Where(archiveConfig => archiveConfig.ArchiveFileSizeMb > 0)
            .GroupBy(archiveConfig => archiveConfig.ArchiveFileSizeMb)
            .Select(group => new { ArchiveFileSizeMb = group.Key, Count = group.Count() })
            .ToDictionaryAsync(
                sizeCount => sizeCount.ArchiveFileSizeMb,
                sizeCount => sizeCount.Count,
                cancellationToken
            );

        var archiveConfigTemplateCountsBySize = await dbRead
            .ArchiveConfigTemplates.Where(template => template.ArchiveFileSizeMb > 0)
            .GroupBy(template => template.ArchiveFileSizeMb)
            .Select(group => new { ArchiveFileSizeMb = group.Key, Count = group.Count() })
            .ToDictionaryAsync(
                sizeCount => sizeCount.ArchiveFileSizeMb,
                sizeCount => sizeCount.Count,
                cancellationToken
            );

        return archiveConfigCountsBySize
            .Keys.Union(archiveConfigTemplateCountsBySize.Keys)
            .Order()
            .Select(archiveFileSizeMb => new ArchivePartSizeChoiceReadModel(
                ArchiveFileSizeMb: archiveFileSizeMb,
                ArchiveConfigCount: archiveConfigCountsBySize.GetValueOrDefault(archiveFileSizeMb),
                ArchiveConfigTemplateCount: archiveConfigTemplateCountsBySize.GetValueOrDefault(
                    archiveFileSizeMb
                )
            ))
            .ToList();
    }

    public async Task<int> GetArchiveConfigCountAsync(
        int archiveFileSizeMb,
        CancellationToken cancellationToken
    )
    {
        return await dbRead.ArchiveConfigs.CountAsync(
            archiveConfig => archiveConfig.ArchiveFileSizeMb == archiveFileSizeMb,
            cancellationToken
        );
    }

    public async Task<int> GetArchiveConfigTemplateCountAsync(
        int archiveFileSizeMb,
        CancellationToken cancellationToken
    )
    {
        return await dbRead.ArchiveConfigTemplates.CountAsync(
            template => template.ArchiveFileSizeMb == archiveFileSizeMb,
            cancellationToken
        );
    }

    public async Task<
        IReadOnlyList<ArchiveConfigUploadHosterRow>
    > GetArchiveConfigUploadHostersAsync(
        int archiveFileSizeMb,
        IReadOnlyList<string> hosterClassNames,
        CancellationToken cancellationToken
    )
    {
        return await dbRead
            .UploadConfigs.Where(uploadConfig =>
                uploadConfig.ArchiveConfig!.ArchiveFileSizeMb == archiveFileSizeMb
                && (
                    hosterClassNames.Contains(uploadConfig.HosterRegistration!.HosterClassName)
                    || hosterClassNames.Contains(uploadConfig.HosterAccountPool!.HosterClassName)
                )
            )
            .Select(uploadConfig => new ArchiveConfigUploadHosterRow(
                ArchiveConfigId: uploadConfig.ArchiveConfig!.Id,
                ReleaseId: uploadConfig.ArchiveConfig.ReleaseId,
                ReleaseName: uploadConfig.ArchiveConfig.Release.Name,
                ArchiveConfigName: uploadConfig.ArchiveConfig.Name,
                HosterClassName: uploadConfig.HosterAccountPoolId == null
                    ? uploadConfig.HosterRegistration!.HosterClassName
                    : uploadConfig.HosterAccountPool!.HosterClassName
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<
        IReadOnlyList<ArchiveConfigTemplateUploadHosterRow>
    > GetArchiveConfigTemplateUploadHostersAsync(
        int archiveFileSizeMb,
        IReadOnlyList<string> hosterClassNames,
        CancellationToken cancellationToken
    )
    {
        return await dbRead
            .UploadConfigTemplates.Where(uploadConfigTemplate =>
                uploadConfigTemplate.ArchiveConfigTemplate!.ArchiveFileSizeMb == archiveFileSizeMb
                && (
                    hosterClassNames.Contains(
                        uploadConfigTemplate.HosterRegistration!.HosterClassName
                    )
                    || hosterClassNames.Contains(
                        uploadConfigTemplate.HosterAccountPool!.HosterClassName
                    )
                )
            )
            .Select(uploadConfigTemplate => new ArchiveConfigTemplateUploadHosterRow(
                ArchiveConfigTemplateId: uploadConfigTemplate.ArchiveConfigTemplate!.Id,
                ReleaseTemplateId: uploadConfigTemplate.ArchiveConfigTemplate.ReleaseTemplateId,
                ReleaseTemplateName: uploadConfigTemplate
                    .ArchiveConfigTemplate
                    .ReleaseTemplate
                    .Name,
                ArchiveConfigTemplateName: uploadConfigTemplate.ArchiveConfigTemplate.Name,
                HosterClassName: uploadConfigTemplate.HosterAccountPoolId == null
                    ? uploadConfigTemplate.HosterRegistration!.HosterClassName
                    : uploadConfigTemplate.HosterAccountPool!.HosterClassName
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> ChangeArchiveConfigFileSizesAsync(
        int currentArchiveFileSizeMb,
        int newArchiveFileSizeMb,
        IReadOnlyList<int> skippedArchiveConfigIds,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .ArchiveConfigs.Where(archiveConfig =>
                archiveConfig.ArchiveFileSizeMb == currentArchiveFileSizeMb
                && !skippedArchiveConfigIds.Contains(archiveConfig.Id)
            )
            .ExecuteUpdateAsync(
                setters =>
                    setters.SetProperty(
                        archiveConfig => archiveConfig.ArchiveFileSizeMb,
                        newArchiveFileSizeMb
                    ),
                cancellationToken
            );
    }

    public async Task<int> ChangeArchiveConfigTemplateFileSizesAsync(
        int currentArchiveFileSizeMb,
        int newArchiveFileSizeMb,
        IReadOnlyList<int> skippedArchiveConfigTemplateIds,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .ArchiveConfigTemplates.Where(template =>
                template.ArchiveFileSizeMb == currentArchiveFileSizeMb
                && !skippedArchiveConfigTemplateIds.Contains(template.Id)
            )
            .ExecuteUpdateAsync(
                setters =>
                    setters.SetProperty(
                        template => template.ArchiveFileSizeMb,
                        newArchiveFileSizeMb
                    ),
                cancellationToken
            );
    }
}
