using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageArchives.ReadModels;

public record ArchiveListItemReadModel(
    int ArchiveId,
    int ReleaseId,
    string ReleaseName,
    int ArchiveConfigId,
    string ArchiveConfigName,
    string ArchiverName,
    ArchiveState ArchiveState,
    string ArchiveFolderPath,
    string? ArchiveStorageFolderName,
    bool HasLocalWorkingCopyFiles,
    DateTime CreatedAt,
    int ArchiveFileCount,
    int UploadCount,
    IReadOnlyList<string> ErrorMessages
);
