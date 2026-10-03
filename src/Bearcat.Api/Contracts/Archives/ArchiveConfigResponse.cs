using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Api.Contracts.Archives;

/// <param name="Id">Id of the archive config.</param>
/// <param name="Name">Display name of the archive config.</param>
/// <param name="ArchiveFilesBasePath">Folder in which new archives are created.</param>
/// <param name="ArchiverName">Class name of the archiver.</param>
/// <param name="ArchiverDisplayName">Display name of the archiver.</param>
/// <param name="ArchiveNamePrefix">File name of the archive without extension. A random name is used when null.</param>
/// <param name="ArchivePassword">Password of the archive, or null.</param>
/// <param name="ArchiveFileSizeMb">Configured part size in MB.</param>
/// <param name="PackReleaseFolderAsRootFolder">True when new archives contain one top level folder named after the release folder. False when the contents of the release folder are packed directly at the top level. Existing archives are not changed.</param>
/// <param name="CreateNonceFile">True when a __nonce.txt file with a random value is packed into every new archive. Existing archives are not changed.</param>
/// <param name="ArchiveFileExtension">File extension of the archiver, for example ".rar".</param>
/// <param name="ArchiveNameWithExtension">ArchiveNamePrefix with the file extension, or null.</param>
/// <param name="Archives">Archives of this config, newest first.</param>
public record ArchiveConfigResponse(
    int Id,
    string Name,
    string ArchiveFilesBasePath,
    string ArchiverName,
    string ArchiverDisplayName,
    string? ArchiveNamePrefix,
    string? ArchivePassword,
    int ArchiveFileSizeMb,
    bool PackReleaseFolderAsRootFolder,
    bool CreateNonceFile,
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
            PackReleaseFolderAsRootFolder: readModel.PackReleaseFolderAsRootFolder,
            CreateNonceFile: readModel.CreateNonceFile,
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
