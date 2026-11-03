namespace Bearcat.Abstractions.Hoster.Results;

public record FileInfoResult(
    bool IsSuccess,
    IReadOnlyList<string> ErrorMessages,
    IReadOnlyDictionary<string, HostedFileInfo> FileInfoPerFileLink
);
