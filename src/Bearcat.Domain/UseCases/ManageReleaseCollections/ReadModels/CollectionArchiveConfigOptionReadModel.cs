namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;

public record CollectionArchiveConfigOptionReadModel(
    string Name,
    int ReleaseCount,
    int ArchiveFileSizeMb
);
