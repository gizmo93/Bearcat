using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Api.Contracts.Archives;

public record ArchiveConfigResponse(
    int Id,
    string Name,
    string ArchiveFilesBasePath,
    string ArchiverName,
    string ArchiverDisplayName,
    string? ArchiveNamePrefix,
    string? ArchivePassword,
    int ArchiveFileSizeMb,
    string ArchiveFileExtension,
    string? ArchiveNameWithExtension,
    IReadOnlyList<ArchiveConfigResponse.ArchiveSummaryResponse> Archives
)
{
    public record ArchiveSummaryResponse(
        int ArchiveId,
        DateTime CreatedAt,
        ArchiveState ArchiveState,
        int ArchiveFileCount,
        IReadOnlyList<string> ErrorMessages
    );

    public static ArchiveConfigResponse FromReadModel(ArchiveConfigReadModel readModel)
    {
        return new ArchiveConfigResponse(
            Id: readModel.ArchiveConfigId,
            Name: readModel.Name,
            ArchiveFilesBasePath: readModel.ArchiveFilesBasePath,
            ArchiverName: readModel.ArchiverName,
            ArchiverDisplayName: readModel.ArchiverDisplayName,
            ArchiveNamePrefix: readModel.ArchiveNamePrefix,
            ArchivePassword: readModel.ArchivePassword,
            ArchiveFileSizeMb: readModel.ArchiveFileSizeMb,
            ArchiveFileExtension: readModel.ArchiveFileExtension,
            ArchiveNameWithExtension: readModel.ArchiveNameWithExtension,
            Archives: readModel
                .ArchiveSummaries.Select(summary => new ArchiveSummaryResponse(
                    ArchiveId: summary.ArchiveId,
                    CreatedAt: summary.CreatedAt,
                    ArchiveState: summary.ArchiveState,
                    ArchiveFileCount: summary.ArchiveFileCount,
                    ErrorMessages: summary.ErrorMessages
                ))
                .ToList()
        );
    }
}
