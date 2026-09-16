namespace Bearcat.Abstractions.Hoster.Results;

public record DownloadFileResult(bool IsSuccess, IReadOnlyList<string> ErrorMessages);
