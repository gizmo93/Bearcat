namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Downloading;

public sealed record FileDownloadResult(
    int ArchiveFileId,
    string TargetFilePath,
    string FileName,
    string HosterName,
    string HosterFileLink,
    bool IsSuccess,
    string? Md5Hash,
    int Attempts,
    bool IsFileMissing,
    IReadOnlyList<string> ErrorMessages
)
{
    public string ErrorText => string.Join(" | ", ErrorMessages);

    public static FileDownloadResult Succeeded(
        PlannedFileDownload download,
        string hosterName,
        string md5Hash,
        int attempts
    ) =>
        new(
            ArchiveFileId: download.ArchiveFileId,
            TargetFilePath: download.TargetFilePath,
            FileName: Path.GetFileName(download.TargetFilePath),
            HosterName: hosterName,
            HosterFileLink: download.UploadedFile.HosterFileLink,
            IsSuccess: true,
            Md5Hash: md5Hash,
            Attempts: attempts,
            IsFileMissing: false,
            ErrorMessages: []
        );

    public static FileDownloadResult Failed(
        PlannedFileDownload download,
        string hosterName,
        int attempts,
        bool isFileMissing,
        IReadOnlyList<string> errorMessages
    ) =>
        new(
            ArchiveFileId: download.ArchiveFileId,
            TargetFilePath: download.TargetFilePath,
            FileName: Path.GetFileName(download.TargetFilePath),
            HosterName: hosterName,
            HosterFileLink: download.UploadedFile.HosterFileLink,
            IsSuccess: false,
            Md5Hash: null,
            Attempts: attempts,
            IsFileMissing: isFileMissing,
            ErrorMessages: errorMessages
        );
}
