namespace Bearcat.Domain.ValueObjects;

public enum LinkCrypterImportContainerStatus
{
    AlreadyInBearcat = 1,
    WaitingForLinks = 2,
    ReadingLinksFailed = 3,
    WaitingForFileCheck = 4,
    Importable = 5,
    NotImportable = 6,
    Imported = 7,
    ImportFailed = 8,
}
