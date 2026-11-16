using Bearcat.Domain.UseCases.ManageArchiveConfigs.PartSizeChange.ReadModels;

namespace Bearcat.Domain.UseCases.ManageArchiveConfigs.PartSizeChange.Repositories;

public interface IArchivePartSizeChangeRepository
{
    Task<IReadOnlyList<ArchivePartSizeChoiceReadModel>> GetPartSizeChoicesAsync(
        CancellationToken cancellationToken
    );

    Task<int> GetArchiveConfigCountAsync(
        int archiveFileSizeMb,
        CancellationToken cancellationToken
    );

    Task<int> GetArchiveConfigTemplateCountAsync(
        int archiveFileSizeMb,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<ArchiveConfigUploadHosterRow>> GetArchiveConfigUploadHostersAsync(
        int archiveFileSizeMb,
        IReadOnlyList<string> hosterClassNames,
        CancellationToken cancellationToken
    );

    Task<
        IReadOnlyList<ArchiveConfigTemplateUploadHosterRow>
    > GetArchiveConfigTemplateUploadHostersAsync(
        int archiveFileSizeMb,
        IReadOnlyList<string> hosterClassNames,
        CancellationToken cancellationToken
    );

    Task<int> ChangeArchiveConfigFileSizesAsync(
        int currentArchiveFileSizeMb,
        int newArchiveFileSizeMb,
        IReadOnlyList<int> skippedArchiveConfigIds,
        CancellationToken cancellationToken
    );

    Task<int> ChangeArchiveConfigTemplateFileSizesAsync(
        int currentArchiveFileSizeMb,
        int newArchiveFileSizeMb,
        IReadOnlyList<int> skippedArchiveConfigTemplateIds,
        CancellationToken cancellationToken
    );
}
