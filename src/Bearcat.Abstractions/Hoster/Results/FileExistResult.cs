namespace Bearcat.Abstractions.Hoster.Results;

public record FileExistResult(
    bool IsSuccess,
    IReadOnlyList<string> ErrorMessages,
    IReadOnlyDictionary<string, bool> StatusPerFileUrl,
    IReadOnlyDictionary<string, int>? DownloadCountPerFileUrl = null
);
