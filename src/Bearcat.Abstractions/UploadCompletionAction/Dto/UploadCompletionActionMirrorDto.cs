namespace Bearcat.Abstractions.UploadCompletionAction.Dto;

public sealed record UploadCompletionActionMirrorDto(
    string HosterClassName,
    string HosterName,
    IReadOnlyList<string> Links,
    long TotalSizeBytes,
    long LargestPartSizeBytes,
    int PartCount,
    IReadOnlyList<UploadCompletionActionLinkCrypterContainerDto> LinkCrypterContainers
);
