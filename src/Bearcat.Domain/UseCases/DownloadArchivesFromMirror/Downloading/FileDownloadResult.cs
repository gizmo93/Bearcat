namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Downloading;

public sealed record FileDownloadResult(
    int ArchiveFileId,
    string TargetFilePath,
    string FileName,
    bool IsSuccess,
    string? Md5Hash,
    string? HosterName,
    int Attempts,
    IReadOnlyList<SourceFailure> SourceFailures
)
{
    public string ErrorText =>
        string.Join(
            "; ",
            SourceFailures.Select(failure =>
                $"{failure.HosterName} ({failure.Attempts} attempts): {string.Join(" | ", failure.ErrorMessages)}"
            )
        );

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
            IsSuccess: true,
            Md5Hash: md5Hash,
            HosterName: hosterName,
            Attempts: attempts,
            SourceFailures: []
        );

    public static FileDownloadResult Failed(
        PlannedFileDownload download,
        IReadOnlyList<SourceFailure> sourceFailures
    ) =>
        new(
            ArchiveFileId: download.ArchiveFileId,
            TargetFilePath: download.TargetFilePath,
            FileName: Path.GetFileName(download.TargetFilePath),
            IsSuccess: false,
            Md5Hash: null,
            HosterName: null,
            Attempts: 0,
            SourceFailures: sourceFailures
        );
}
