namespace BearCat.Core.Domain.Abstractions.Hoster.Results;

public record UploadFileResult(
    bool IsSuccess,
    string SourceFilePath,
    IReadOnlyList<string> ErrorMessages,
    string? FileUrl);
