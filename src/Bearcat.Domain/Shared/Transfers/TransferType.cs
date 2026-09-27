namespace Bearcat.Domain.Shared.Transfers;

public enum TransferType
{
    Upload = 1,
    MirrorDownload = 2,
    RemoteDownload = 3,
    RemoteDownloadVerification = 4,
    RemoteDownloadExtraction = 5,
}
