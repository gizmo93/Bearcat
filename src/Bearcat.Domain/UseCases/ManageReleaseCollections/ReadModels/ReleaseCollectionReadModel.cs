namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;

public record ReleaseCollectionReadModel(
    int ReleaseCollectionId,
    string Name,
    string Key,
    int ReleaseGroupId,
    string ReleaseGroupName,
    int ReleaseCount,
    DateTime CreatedAt
);
