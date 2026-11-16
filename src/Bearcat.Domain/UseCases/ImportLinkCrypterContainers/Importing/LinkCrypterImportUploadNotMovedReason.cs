namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Importing;

public enum LinkCrypterImportUploadNotMovedReason
{
    NoReleaseWithArchiveName = 1,
    UploadedFileWithoutArchiveFile = 2,
    NoUploadConfigForHoster = 3,
    ArchiveFileNamesDiffer = 4,
    SameLinksAlreadyOnRelease = 5,
}
