namespace Bearcat.Domain.Abstractions.Archiver;

public record ArchiveResult(
    bool IsSuccess,
    IReadOnlyList<string> CreatedFileNames,
    IReadOnlyList<string>? ErrorMessages);
