namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Reimporting;

public sealed record LinkCrypterImportReimportMovedUploadReadModel(
    string UploadConfigName,
    int FileCount
);
