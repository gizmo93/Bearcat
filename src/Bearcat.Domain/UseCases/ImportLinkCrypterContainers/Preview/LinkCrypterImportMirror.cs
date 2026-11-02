namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;

public sealed record LinkCrypterImportMirror(
    int MirrorNumber,
    string? HosterClassName,
    IReadOnlyList<LinkCrypterImportMirrorFile> Files,
    LinkCrypterImportMirrorDecision? Decision,
    LinkCrypterImportUploadByBearcatReason? UploadByBearcatReason,
    int? UploadConfigTemplateId
);
