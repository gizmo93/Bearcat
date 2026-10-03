using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.ReadModels;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;

public record ArchiveConfigTemplateReadModel(
    int ArchiveConfigTemplateId,
    string Name,
    string ArchiveFilesBasePath,
    string ArchiverName,
    string ArchiverDisplayName,
    string? ArchivePassword,
    int ArchiveFileSizeMb,
    bool UseReleaseNameAsArchiveName,
    bool PackReleaseFolderAsRootFolder,
    bool CreateNonceFile,
    int UploadConfigTemplateCount,
    IReadOnlyList<AssignedAdditionalArchiveContentReadModel> AdditionalArchiveContents
);
