namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Reimporting;

public sealed record LinkCrypterImportReimportContainerNotConvertedReadModel(
    string ContainerUrl,
    LinkCrypterImportReimportContainerNotConvertedReason Reason
);
