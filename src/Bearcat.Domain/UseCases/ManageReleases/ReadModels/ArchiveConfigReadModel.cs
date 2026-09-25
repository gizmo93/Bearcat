using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.ReadModels;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ArchiveConfigReadModel(
    int ArchiveConfigId,
    string ArchiveFilesBasePath,
    string ArchiverName,
    string ArchiverDisplayName,
    string? ArchiveNamePrefix,
    string? ArchivePassword,
    int ArchiveFileSizeMb,
    string ArchiveFileExtension,
    string Name,
    IReadOnlyList<ArchiveConfigReadModel.ArchiveSummary> ArchiveSummaries,
    IReadOnlyList<AssignedAdditionalArchiveContentReadModel> AdditionalArchiveContents
)
{
    public record ArchiveSummary(
        int ArchiveId,
        DateTime CreatedAt,
        ArchiveState ArchiveState,
        int ArchiveFileCount,
        IReadOnlyList<string> ErrorMessages
    );

    public string? ArchiveNameWithExtension =>
        ArchiveNamePrefix is null ? null : $"{ArchiveNamePrefix}{ArchiveFileExtension}";
}
