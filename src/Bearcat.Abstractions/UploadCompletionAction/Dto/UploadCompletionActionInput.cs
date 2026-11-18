namespace Bearcat.Abstractions.UploadCompletionAction.Dto;

public sealed record UploadCompletionActionInput(
    UploadCompletionActionReleaseDto Release,
    IReadOnlyList<string> CollectionReleaseNames,
    bool IsReupload,
    IReadOnlyList<UploadCompletionActionMirrorDto> Mirrors,
    IReadOnlyList<UploadCompletionActionLinkCrypterContainerDto> AllHostersLinkCrypterContainers,
    IReadOnlyList<UploadCompletionActionImageDto> Images
);
