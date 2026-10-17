using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageArchives.ReadModels;

public record ArchiveReadModel(
    int ArchiveId,
    ArchiveState ArchiveState,
    string ArchiveFolderPath,
    string? ArchiveStorageFolderName,
    bool HasLocalWorkingCopyFiles,
    DateTime CreatedAt,
    IReadOnlyList<ArchiveReadModel.ArchiveFileReadModel> Files
)
{
    public record ArchiveFileReadModel(int ArchiveFileId, string FullFileName);
}
