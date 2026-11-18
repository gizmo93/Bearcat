namespace Bearcat.Domain.UseCases.RunUploadCompletionActions.Models;

public sealed record UploadCompletionActionDueRelease(
    int ReleaseId,
    bool IsReupload,
    DateTime NewestUploadedAt
);
