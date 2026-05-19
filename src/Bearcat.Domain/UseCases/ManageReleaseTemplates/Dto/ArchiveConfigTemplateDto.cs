namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.Dto;

public record ArchiveConfigTemplateDto(
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
