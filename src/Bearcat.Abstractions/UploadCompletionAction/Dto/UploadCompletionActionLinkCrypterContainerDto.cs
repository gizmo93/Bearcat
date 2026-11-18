namespace Bearcat.Abstractions.UploadCompletionAction.Dto;

public sealed record UploadCompletionActionLinkCrypterContainerDto(
    string LinkCrypterClassName,
    string LinkCrypterName,
    string Url
);
