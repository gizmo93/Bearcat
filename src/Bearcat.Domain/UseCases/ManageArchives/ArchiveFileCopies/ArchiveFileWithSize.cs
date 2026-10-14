using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageArchives.ArchiveFileCopies;

public sealed record ArchiveFileWithSize(ArchiveFile ArchiveFile, long SizeBytes);
