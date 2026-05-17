namespace Bearcat.Abstractions.Archiver;

public record ArchiveResult(
    bool IsSuccess,
    IReadOnlyList<string> CreatedFileNames,
    IReadOnlyList<string>? ErrorMessages
);
