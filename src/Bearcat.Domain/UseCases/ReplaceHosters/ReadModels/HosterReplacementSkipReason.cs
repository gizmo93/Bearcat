namespace Bearcat.Domain.UseCases.ReplaceHosters.ReadModels;

public enum HosterReplacementSkipReason
{
    DuplicateOnReplacementHoster = 1,
    CollectionUploadSlotPartiallySkipped = 2,
    DeletionBlockedByOtherCollectionUploadSlotMember = 3,
    DeletionBlockedByRunningUpload = 4,
}
