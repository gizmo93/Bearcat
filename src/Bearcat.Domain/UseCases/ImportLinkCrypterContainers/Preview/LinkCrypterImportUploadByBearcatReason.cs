namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;

public enum LinkCrypterImportUploadByBearcatReason
{
    SomeFilesOffline = 1,
    FileStatesUnknown = 2,
    FileNamesAreNotArchiveParts = 3,
    FileNamesDifferFromFirstAdoptedMirror = 4,
}
