namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Downloading;

public sealed record SourceFailure(
    string HosterName,
    string HosterFileLink,
    int Attempts,
    bool IsFileMissing,
    IReadOnlyList<string> ErrorMessages
);
