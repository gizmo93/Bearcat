namespace Bearcat.Domain.UseCases.ManageArchives.Dto;

public record ArchiveDto(
    int ArchiveId,
    string ArchiveFolderPath,
    DateTime CreatedAt,
    IReadOnlyList<ArchiveDto.ArchiveFileDto> Files)
{
    public record ArchiveFileDto(
        int ArchiveFileId,
        string FullFileName);
}
