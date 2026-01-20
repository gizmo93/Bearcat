namespace BearCat.Core.Domain.UseCases.ManageReleases.Dto;

public record ArchiveConfigDto(
    int ArchiveConfigId,
    string ArchiveFilesBasePath,
    string ArchiverName,
    string ArchiveNamePrefix,
    string? ArchivePassword,
    int ArchiveFileSizeMb,
    IReadOnlyList<ArchiveConfigDto.ArchiveSummary> ArchiveSummaries)
{
    public record ArchiveSummary(int ArchiveId, int ArchiveFileCount);
}
