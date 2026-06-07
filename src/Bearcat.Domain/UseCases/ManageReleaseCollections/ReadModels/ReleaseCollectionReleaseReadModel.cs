using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;

public record ReleaseCollectionReleaseReadModel(
    int ReleaseId,
    string Name,
    ReleaseType ReleaseType,
    DateTime CreatedAt
);
