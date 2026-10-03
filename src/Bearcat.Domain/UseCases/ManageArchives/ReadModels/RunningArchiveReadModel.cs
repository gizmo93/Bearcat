using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageArchives.ReadModels;

public record RunningArchiveReadModel(
    int ArchiveId,
    int ReleaseId,
    string ReleaseName,
    string ArchiveConfigName,
    string ArchiverName,
    ArchiveState ArchiveState
);
