using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using Bearcat.Domain.UseCases.ManageUploadConfigs.UploadSources;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates;

public class ReleaseTemplateService(IReleaseTemplateWriteRepository writeRepository)
{
    public async Task<int> CreateAsync(
        string name,
        ReleaseType releaseType,
        ReleaseContentType releaseContentType,
        int releaseGroupId,
        ReleaseCollectionDetectionMode releaseCollectionDetectionMode =
            ReleaseCollectionDetectionMode.Disabled,
        string? releaseCollectionPattern = null,
        string? releaseCollectionKeyTemplate = null,
        string? releaseCollectionNameTemplate = null,
        CancellationToken cancellationToken = default
    )
    {
        var releaseTemplate = new ReleaseTemplate
        {
            Name = name,
            ReleaseType = releaseType,
            ReleaseContentType = releaseContentType,
            ReleaseGroupId = releaseGroupId,
            ReleaseCollectionDetectionMode = releaseCollectionDetectionMode,
            ReleaseCollectionPattern = releaseCollectionPattern.TrimOrNullWhenWhitespace(),
            ReleaseCollectionKeyTemplate = releaseCollectionKeyTemplate.TrimOrNullWhenWhitespace(),
            ReleaseCollectionNameTemplate =
                releaseCollectionNameTemplate.TrimOrNullWhenWhitespace(),
        };

        UnmanagedArchiveConfigTemplates.AddIfMissing(releaseTemplate);

        writeRepository.Add(releaseTemplate);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return releaseTemplate.Id;
    }

    public async Task UpdateAsync(
        int releaseTemplateId,
        string name,
        ReleaseType releaseType,
        ReleaseContentType releaseContentType,
        int releaseGroupId,
        ReleaseCollectionDetectionMode releaseCollectionDetectionMode =
            ReleaseCollectionDetectionMode.Disabled,
        string? releaseCollectionPattern = null,
        string? releaseCollectionKeyTemplate = null,
        string? releaseCollectionNameTemplate = null,
        CancellationToken cancellationToken = default
    )
    {
        var releaseTemplate = await writeRepository.GetByIdWithChildrenAsync(
            releaseTemplateId,
            cancellationToken
        );

        if (releaseTemplate.ReleaseType != releaseType)
        {
            throw new InvalidOperationException("Release template type cannot be changed.");
        }

        if (
            releaseTemplate.UploadConfigTemplates.Any(template =>
                template.UploadSource is UploadSource.VideoFiles
            ) && !UploadSourceValidator.AllowsVideoFileUploads(releaseType, releaseContentType)
        )
        {
            throw new InvalidUploadSourceException(
                "The content type must stay movie or TV show episode while the release template has video file upload config templates."
            );
        }

        releaseTemplate.Name = name;
        releaseTemplate.ReleaseContentType = releaseContentType;
        releaseTemplate.ReleaseGroupId = releaseGroupId;
        releaseTemplate.ReleaseCollectionDetectionMode = releaseCollectionDetectionMode;
        releaseTemplate.ReleaseCollectionPattern =
            releaseCollectionPattern.TrimOrNullWhenWhitespace();
        releaseTemplate.ReleaseCollectionKeyTemplate =
            releaseCollectionKeyTemplate.TrimOrNullWhenWhitespace();
        releaseTemplate.ReleaseCollectionNameTemplate =
            releaseCollectionNameTemplate.TrimOrNullWhenWhitespace();

        if (releaseCollectionDetectionMode == ReleaseCollectionDetectionMode.Disabled)
        {
            releaseTemplate.CollectionImageUploadConfigTemplates.Clear();
        }

        UnmanagedArchiveConfigTemplates.AddIfMissing(releaseTemplate);

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        int releaseTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        var releaseTemplate = await writeRepository.GetByIdWithChildrenAsync(
            releaseTemplateId,
            cancellationToken
        );

        writeRepository.Remove(releaseTemplate);

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task AddUnmanagedArchiveConfigTemplateIfMissingAsync(
        int releaseTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        var releaseTemplate = await writeRepository.GetByIdWithChildrenAsync(
            releaseTemplateId,
            cancellationToken
        );

        UnmanagedArchiveConfigTemplates.AddIfMissing(releaseTemplate);

        await writeRepository.SaveChangesAsync(cancellationToken);
    }
}
