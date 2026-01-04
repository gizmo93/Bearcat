namespace BearCat.Core.Domain.Abstractions.Hoster.Results;

public record UploadFileResult(bool IsSuccess, IReadOnlyList<string> ErrorMessages, string? FileUrl);
