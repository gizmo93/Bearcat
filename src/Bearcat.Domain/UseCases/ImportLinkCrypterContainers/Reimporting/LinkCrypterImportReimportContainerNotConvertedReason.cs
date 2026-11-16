namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Reimporting;

public enum LinkCrypterImportReimportContainerNotConvertedReason
{
    CollectionUploadSlotHasContainerOfLinkCrypterAccount = 1,
    CollectionUploadSlotUsesLinkCrypterAccountForReleaseContainers = 2,
}
