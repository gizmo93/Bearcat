namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.ArchiveConfigTemplates;

public sealed record ArchiveConfigTemplateSettings(
    string Name,
    string ArchiveFilesBasePath,
    string ArchiverName,
    string? ArchivePassword,
    int ArchiveFileSizeMb,
    bool UseReleaseNameAsArchiveName,
    bool PackReleaseFolderAsRootFolder,
    bool CreateNonceFile,
    IReadOnlyList<int> AdditionalArchiveContentIds
);
