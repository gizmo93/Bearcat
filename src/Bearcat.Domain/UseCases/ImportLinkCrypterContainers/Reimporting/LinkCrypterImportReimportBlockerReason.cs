namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Reimporting;

public enum LinkCrypterImportReimportBlockerReason
{
    UploadInProgress = 1,
    ArchiveInProgress = 2,
    NotImportable = 3,
    ImportFailed = 4,
}
