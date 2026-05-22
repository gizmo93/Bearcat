namespace Bearcat.Domain.UseCases.ManageArchives.ReadModels;

public record ArchiveReadModel(
    int ArchiveId,
    string ArchiveFolderPath,
    DateTime CreatedAt,
    IReadOnlyList<ArchiveReadModel.ArchiveFileReadModel> Files
)
{
    public record ArchiveFileReadModel(int ArchiveFileId, string FullFileName);
}
