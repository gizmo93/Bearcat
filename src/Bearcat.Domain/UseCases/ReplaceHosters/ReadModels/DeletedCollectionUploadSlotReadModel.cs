namespace Bearcat.Domain.UseCases.ReplaceHosters.ReadModels;

public sealed record DeletedCollectionUploadSlotReadModel(
    int CollectionUploadSlotId,
    string CollectionUploadSlotName,
    int ReleaseCollectionId,
    string ReleaseCollectionName,
    int UploadConfigCount
);
