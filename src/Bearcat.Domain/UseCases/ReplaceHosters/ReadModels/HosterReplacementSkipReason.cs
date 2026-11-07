namespace Bearcat.Domain.UseCases.ReplaceHosters.ReadModels;

public enum HosterReplacementSkipReason
{
    DuplicateOnReplacementHoster = 1,
    CollectionUploadSlotPartiallySkipped = 2,
    DeletionBlockedByCollectionUploadSlot = 3,
    DeletionBlockedByRunningUpload = 4,
}
