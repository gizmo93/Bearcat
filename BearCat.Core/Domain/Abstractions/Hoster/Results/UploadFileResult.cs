using BearCat.Core.Domain.Entities;

namespace BearCat.Core.Domain.Abstractions.Hoster.Results;

public record UploadFileResult(
    bool IsSuccess,
    ArchiveFile ArchiveFile,
    IReadOnlyList<string> ErrorMessages,
    string? FileUrl);
