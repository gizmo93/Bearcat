using Bearcat.Domain.Entities;

namespace Bearcat.Domain.Abstractions.Hoster.Results;

public record UploadFileResult(
    bool IsSuccess,
    ArchiveFile ArchiveFile,
    IReadOnlyList<string> ErrorMessages,
    string? FileUrl);
