using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Reimporting;

public sealed record LinkCrypterImportReimportReleaseReadModel(
    LinkCrypterImportReleasePreview Preview,
    IReadOnlyList<LinkCrypterImportReimportMovedUploadReadModel> MovedUploads
);
