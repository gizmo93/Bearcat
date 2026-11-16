using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.UseCases.ManageArchiveConfigs.PartSizeChange.Dto;
using Bearcat.Domain.UseCases.ManageArchiveConfigs.PartSizeChange.ReadModels;
using Bearcat.Domain.UseCases.ManageArchiveConfigs.PartSizeChange.Repositories;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageArchiveConfigs.PartSizeChange;

public class ArchivePartSizeChangeService(
    IArchivePartSizeChangeRepository repository,
    IHosterFactory hosterFactory,
    IApplicationConfigurationProvider configurationProvider,
    ILogger<ArchivePartSizeChangeService> logger
)
{
    public async Task<IReadOnlyList<ArchivePartSizeChoiceReadModel>> GetPartSizeChoicesAsync(
        CancellationToken cancellationToken
    )
    {
        return await repository.GetPartSizeChoicesAsync(cancellationToken);
    }

    public async Task<ArchivePartSizeChangePreview> GetPreviewAsync(
        ArchivePartSizeChangeRequest request,
        CancellationToken cancellationToken
    )
    {
        EnsureArchiveFileSizesAreManaged(request);

        var hostersByClassName = GetHostersWithMaxArchiveFileSizeBelow(
            request.NewArchiveFileSizeMb
        );

        var skippedArchiveConfigs = await GetSkippedArchiveConfigsAsync(
            currentArchiveFileSizeMb: request.CurrentArchiveFileSizeMb,
            hostersByClassName: hostersByClassName,
            cancellationToken: cancellationToken
        );

        var skippedArchiveConfigTemplates = request.IncludeArchiveConfigTemplates
            ? await GetSkippedArchiveConfigTemplatesAsync(
                currentArchiveFileSizeMb: request.CurrentArchiveFileSizeMb,
                hostersByClassName: hostersByClassName,
                cancellationToken: cancellationToken
            )
            : [];

        var archiveConfigCount = await repository.GetArchiveConfigCountAsync(
            request.CurrentArchiveFileSizeMb,
            cancellationToken
        );

        var archiveConfigTemplateCount = request.IncludeArchiveConfigTemplates
            ? await repository.GetArchiveConfigTemplateCountAsync(
                request.CurrentArchiveFileSizeMb,
                cancellationToken
            )
            : 0;

        return new ArchivePartSizeChangePreview(
            ArchiveConfigCountToChange: archiveConfigCount - skippedArchiveConfigs.Count,
            ArchiveConfigTemplateCountToChange: archiveConfigTemplateCount
                - skippedArchiveConfigTemplates.Count,
            SkippedArchiveConfigs: skippedArchiveConfigs,
            SkippedArchiveConfigTemplates: skippedArchiveConfigTemplates
        );
    }

    public async Task<ArchivePartSizeChangeResult> ApplyAsync(
        ArchivePartSizeChangeRequest request,
        CancellationToken cancellationToken
    )
    {
        EnsureArchiveFileSizesAreManaged(request);

        var hostersByClassName = GetHostersWithMaxArchiveFileSizeBelow(
            request.NewArchiveFileSizeMb
        );

        var skippedArchiveConfigs = await GetSkippedArchiveConfigsAsync(
            currentArchiveFileSizeMb: request.CurrentArchiveFileSizeMb,
            hostersByClassName: hostersByClassName,
            cancellationToken: cancellationToken
        );

        var skippedArchiveConfigTemplates = request.IncludeArchiveConfigTemplates
            ? await GetSkippedArchiveConfigTemplatesAsync(
                currentArchiveFileSizeMb: request.CurrentArchiveFileSizeMb,
                hostersByClassName: hostersByClassName,
                cancellationToken: cancellationToken
            )
            : [];

        var changedArchiveConfigCount = await repository.ChangeArchiveConfigFileSizesAsync(
            currentArchiveFileSizeMb: request.CurrentArchiveFileSizeMb,
            newArchiveFileSizeMb: request.NewArchiveFileSizeMb,
            skippedArchiveConfigIds: skippedArchiveConfigs.Select(s => s.ArchiveConfigId).ToList(),
            cancellationToken: cancellationToken
        );

        var changedArchiveConfigTemplateCount = request.IncludeArchiveConfigTemplates
            ? await repository.ChangeArchiveConfigTemplateFileSizesAsync(
                currentArchiveFileSizeMb: request.CurrentArchiveFileSizeMb,
                newArchiveFileSizeMb: request.NewArchiveFileSizeMb,
                skippedArchiveConfigTemplateIds: skippedArchiveConfigTemplates
                    .Select(s => s.ArchiveConfigTemplateId)
                    .ToList(),
                cancellationToken: cancellationToken
            )
            : 0;

        logger.LogInformation(
            "Changed archive part size from {CurrentArchiveFileSize} to {NewArchiveFileSize}: {ChangedArchiveConfigCount} archive configs and {ChangedArchiveConfigTemplateCount} archive config templates changed, {SkippedArchiveConfigCount} archive configs and {SkippedArchiveConfigTemplateCount} archive config templates skipped because of hoster file size limits",
            request.CurrentArchiveFileSizeMb,
            request.NewArchiveFileSizeMb,
            changedArchiveConfigCount,
            changedArchiveConfigTemplateCount,
            skippedArchiveConfigs.Count,
            skippedArchiveConfigTemplates.Count
        );

        return new ArchivePartSizeChangeResult(
            ChangedArchiveConfigCount: changedArchiveConfigCount,
            ChangedArchiveConfigTemplateCount: changedArchiveConfigTemplateCount,
            SkippedArchiveConfigCount: skippedArchiveConfigs.Count,
            SkippedArchiveConfigTemplateCount: skippedArchiveConfigTemplates.Count
        );
    }

    private static void EnsureArchiveFileSizesAreManaged(ArchivePartSizeChangeRequest request)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.CurrentArchiveFileSizeMb);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.NewArchiveFileSizeMb);
    }

    private async Task<List<SkippedArchiveConfigReadModel>> GetSkippedArchiveConfigsAsync(
        int currentArchiveFileSizeMb,
        Dictionary<string, HosterWithMaxArchiveFileSize> hostersByClassName,
        CancellationToken cancellationToken
    )
    {
        if (hostersByClassName.Count == 0)
        {
            return [];
        }

        var rows = await repository.GetArchiveConfigUploadHostersAsync(
            archiveFileSizeMb: currentArchiveFileSizeMb,
            hosterClassNames: hostersByClassName.Keys.ToList(),
            cancellationToken: cancellationToken
        );

        return rows.Select(row => new SkippedArchiveConfigReadModel(
                ArchiveConfigId: row.ArchiveConfigId,
                ReleaseId: row.ReleaseId,
                ReleaseName: row.ReleaseName,
                ArchiveConfigName: row.ArchiveConfigName,
                HosterName: hostersByClassName[row.HosterClassName].HosterName,
                MaxArchiveFileSize: hostersByClassName[row.HosterClassName].MaxArchiveFileSize
            ))
            .GroupBy(skipped => skipped.ArchiveConfigId)
            .Select(group => group.MinBy(skipped => skipped.MaxArchiveFileSize)!)
            .OrderBy(skipped => skipped.ReleaseName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(skipped => skipped.ArchiveConfigName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<
        List<SkippedArchiveConfigTemplateReadModel>
    > GetSkippedArchiveConfigTemplatesAsync(
        int currentArchiveFileSizeMb,
        Dictionary<string, HosterWithMaxArchiveFileSize> hostersByClassName,
        CancellationToken cancellationToken
    )
    {
        if (hostersByClassName.Count == 0)
        {
            return [];
        }

        var rows = await repository.GetArchiveConfigTemplateUploadHostersAsync(
            archiveFileSizeMb: currentArchiveFileSizeMb,
            hosterClassNames: hostersByClassName.Keys.ToList(),
            cancellationToken: cancellationToken
        );

        return rows.Select(row => new SkippedArchiveConfigTemplateReadModel(
                ArchiveConfigTemplateId: row.ArchiveConfigTemplateId,
                ReleaseTemplateId: row.ReleaseTemplateId,
                ReleaseTemplateName: row.ReleaseTemplateName,
                ArchiveConfigTemplateName: row.ArchiveConfigTemplateName,
                HosterName: hostersByClassName[row.HosterClassName].HosterName,
                MaxArchiveFileSize: hostersByClassName[row.HosterClassName].MaxArchiveFileSize
            ))
            .GroupBy(skipped => skipped.ArchiveConfigTemplateId)
            .Select(group => group.MinBy(skipped => skipped.MaxArchiveFileSize)!)
            .OrderBy(skipped => skipped.ReleaseTemplateName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(skipped => skipped.ArchiveConfigTemplateName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private Dictionary<string, HosterWithMaxArchiveFileSize> GetHostersWithMaxArchiveFileSizeBelow(
        int archiveFileSize
    )
    {
        var partSizeUnit = configurationProvider.GetValue<
            ArchiveCreationConfiguration,
            ArchivePartSizeUnit
        >(c => c.PartSizeUnit);

        var hostersByClassName = new Dictionary<string, HosterWithMaxArchiveFileSize>();

        foreach (var (hosterClassName, hoster) in hosterFactory.GetHostersByName())
        {
            if (hoster is not IHosterWithFileSizeLimit hosterWithFileSizeLimit)
            {
                continue;
            }

            var maxArchiveFileSize = ArchivePartSizeConversion.ConvertMegabytesToPartSizeUnit(
                megabytes: hosterWithFileSizeLimit.MaxFileSizeMb,
                partSizeUnit: partSizeUnit
            );

            if (maxArchiveFileSize < archiveFileSize)
            {
                hostersByClassName[hosterClassName] = new HosterWithMaxArchiveFileSize(
                    HosterName: hoster.Name,
                    MaxArchiveFileSize: maxArchiveFileSize
                );
            }
        }

        return hostersByClassName;
    }

    private sealed record HosterWithMaxArchiveFileSize(string HosterName, int MaxArchiveFileSize);
}
