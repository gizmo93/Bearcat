namespace Bearcat.Abstractions.UploadCompletionAction.Dto;

public sealed record UploadCompletionActionInput(
    UploadCompletionActionReleaseDto Release,
    bool IsReupload,
    IReadOnlyList<UploadCompletionActionMirrorDto> Mirrors,
    IReadOnlyList<UploadCompletionActionLinkCrypterContainerDto> AllHostersLinkCrypterContainers,
    IReadOnlyList<UploadCompletionActionImageDto> Images
);
