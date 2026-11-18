namespace Bearcat.Abstractions.UploadCompletionAction.Dto;

public sealed record UploadCompletionActionReleaseDto(
    string Name,
    string? ContentKind,
    string ReleaseGroupName,
    string? Nfo
);
