namespace Bearcat.Abstractions.Archiver;

public record ArchiveExtractionResult(bool IsSuccess, IReadOnlyList<string> ErrorMessages);
