namespace Bearcat.Domain.UseCases.FirstStartSetup.Sources;

public sealed record FirstStartFolderSourceInput(
    string BasePath,
    string? FolderNamePattern,
    int ReleaseTemplateId,
    bool ExtractArchivesBeforeReleaseCreation,
    FirstStartExistingFolderHandling ExistingFolderHandling,
    string? TestFolderPath
);
