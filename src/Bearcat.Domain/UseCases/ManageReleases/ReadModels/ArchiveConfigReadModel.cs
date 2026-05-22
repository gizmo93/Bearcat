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
    IReadOnlyList<ArchiveConfigReadModel.ArchiveSummary> ArchiveSummaries
)
{
    public record ArchiveSummary(int ArchiveId, int ArchiveFileCount);

    public string? ArchiveNameWithExtension =>
        ArchiveNamePrefix is null ? null : $"{ArchiveNamePrefix}{ArchiveFileExtension}";
}
