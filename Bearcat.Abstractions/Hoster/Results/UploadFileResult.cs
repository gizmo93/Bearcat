using Bearcat.Abstractions.Hoster.Dto;

namespace Bearcat.Abstractions.Hoster.Results;

public record UploadFileResult(
    bool IsSuccess,
    FileDto FileDto,
    IReadOnlyList<string> ErrorMessages,
    string? FileUrl);
