namespace BearCat.Core.Domain.UseCases.ManageReleases.Dto;

public record ArchiveConfigDto(
    int ArchiveConfigId,
    string ArchiveFilesBasePath,
    string ArchiverName,
    string ArchiverDisplayName,
    string ArchiveNamePrefix,
    string? ArchivePassword,
    int ArchiveFileSizeMb,
    string ArchiveFileExtension,
    string Name,
    IReadOnlyList<ArchiveConfigDto.ArchiveSummary> ArchiveSummaries)
{
    public record ArchiveSummary(int ArchiveId, int ArchiveFileCount);

    public string ArchiveNameWithExtension => $"{ArchiveNamePrefix}{ArchiveFileExtension}";
}
