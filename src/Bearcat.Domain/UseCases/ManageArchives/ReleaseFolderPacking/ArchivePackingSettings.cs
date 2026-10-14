using Bearcat.Abstractions.Archiver;

namespace Bearcat.Domain.UseCases.ManageArchives.ReleaseFolderPacking;

public record ArchivePackingSettings(int ArchiveFileSizeMb, ArchiveOptions Options);
