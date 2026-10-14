using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageArchives.ArchiveFileCopies;

public sealed record CopiedArchiveFile(ArchiveFile ArchiveFile, string CopiedFilePath);
