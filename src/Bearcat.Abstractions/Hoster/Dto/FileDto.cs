namespace Bearcat.Abstractions.Hoster.Dto;

public record FileDto(
    int Id,
    string FullFileName,
    int UploadId,
    string? FolderId = null,
    bool PremiumOnlyDownload = false,
    string? Md5Hash = null
);
