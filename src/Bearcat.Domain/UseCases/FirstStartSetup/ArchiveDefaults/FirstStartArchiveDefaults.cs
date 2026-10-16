namespace Bearcat.Domain.UseCases.FirstStartSetup.ArchiveDefaults;

public sealed record FirstStartArchiveDefaults(
    string ArchiverName,
    int ArchiveFileSizeMb,
    string SuggestedArchiveOutputFolderPath,
    bool SuggestedArchiveOutputFolderExists
);
