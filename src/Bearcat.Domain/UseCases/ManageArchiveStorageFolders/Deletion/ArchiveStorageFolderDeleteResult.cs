namespace Bearcat.Domain.UseCases.ManageArchiveStorageFolders.Deletion;

public record ArchiveStorageFolderDeleteResult(bool IsDeleted, int StoredArchiveCount);
