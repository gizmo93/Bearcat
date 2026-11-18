namespace Bearcat.Abstractions.UploadCompletionAction.Dto;

public sealed record UploadCompletionActionImageDto(
    string ImageHosterClassName,
    string ImageHosterName,
    string Url,
    bool IsCover
);
