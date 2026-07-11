using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;

public record ReleaseCollectionDetailReadModel(
    int ReleaseCollectionId,
    string Name,
    string Key,
    ReleaseContentType ReleaseContentType,
    int ReleaseGroupId,
    string ReleaseGroupName,
    string? PrimaryLanguageCode,
    DateTime CreatedAt,
    IReadOnlyList<CollectionUploadSlotReadModel> UploadSlots,
    IReadOnlyList<ReleaseCollectionReleaseReadModel> Releases,
    ReleaseCollectionMetadataReadModel? Metadata
);
