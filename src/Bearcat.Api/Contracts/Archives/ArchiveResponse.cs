using Bearcat.Domain.UseCases.ManageArchives.ReadModels;

namespace Bearcat.Api.Contracts.Archives;

public record ArchiveResponse(
    int Id,
    string ArchiveFolderPath,
    DateTime CreatedAt,
    IReadOnlyList<ArchiveResponse.ArchiveFileResponse> Files
)
{
    public record ArchiveFileResponse(int Id, string FullFileName);

    public static ArchiveResponse FromReadModel(ArchiveReadModel readModel)
    {
        return new ArchiveResponse(
            Id: readModel.ArchiveId,
            ArchiveFolderPath: readModel.ArchiveFolderPath,
            CreatedAt: readModel.CreatedAt,
            Files: readModel
                .Files.Select(file => new ArchiveFileResponse(
                    Id: file.ArchiveFileId,
                    FullFileName: file.FullFileName
                ))
                .ToList()
        );
    }
}
