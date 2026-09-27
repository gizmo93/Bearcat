namespace Bearcat.Abstractions.Archiver;

public record ArchiveToExtract(string FirstVolumeFilePath, IReadOnlyList<string> VolumeFilePaths);
