namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record UnmanagedArchiveFolderReadModel(
    string ArchiveFolderPath,
    string? ArchiveStorageFolderName,
    bool HasLocalWorkingCopyFiles
);
