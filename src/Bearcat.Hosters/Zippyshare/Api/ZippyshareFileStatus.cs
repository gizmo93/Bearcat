namespace Bearcat.Hosters.Zippyshare.Api;

public record ZippyshareFileStatus(
    bool IsOnline,
    string? FileName,
    long? SizeBytes,
    int? DownloadCount
);
