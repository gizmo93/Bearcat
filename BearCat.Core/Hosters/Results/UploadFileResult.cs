namespace BearCat.Core.Hosters.Results;

public record UploadFileResult(bool IsSuccess, IReadOnlyList<string> ErrorMessages, string? FileUrl);