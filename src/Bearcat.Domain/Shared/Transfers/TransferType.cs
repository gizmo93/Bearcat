namespace Bearcat.Domain.Shared.Transfers;

public enum TransferType
{
    Upload = 1,
    MirrorDownload = 2,
    RemoteDownload = 3,
    RemoteDownloadVerification = 4,
    RemoteDownloadExtraction = 5,
    ArchiveCreation = 6,
    ArchiveHashing = 7,
    ArchiveHashChange = 8,
    ReleaseFolderVerification = 9,
    ReleaseFolderExtraction = 10,
}
