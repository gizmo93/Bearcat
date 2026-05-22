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
    int UploadConfigTemplateCount
);
