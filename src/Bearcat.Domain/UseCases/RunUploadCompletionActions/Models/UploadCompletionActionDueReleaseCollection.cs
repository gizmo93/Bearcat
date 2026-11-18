namespace Bearcat.Domain.UseCases.RunUploadCompletionActions.Models;

public sealed record UploadCompletionActionDueReleaseCollection(
    int ReleaseCollectionId,
    bool IsReupload,
    DateTime NewestUploadedAt
);
