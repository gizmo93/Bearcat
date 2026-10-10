namespace Bearcat.Domain.UseCases.ManageArchiveStorageFolders.Validation;

public enum ArchiveStorageFolderValidationError
{
    NameRequired = 1,
    NameTooLong = 2,
    NameAlreadyExists = 3,
    PathRequired = 4,
    PathTooLong = 5,
    PathNotAbsolute = 6,
    PathNotFound = 7,
    PathAlreadyExists = 8,
    PathOverlapsWorkingDirectory = 9,
    PathChangeWithStoredArchives = 10,
    MinimumFreeSpaceNegative = 11,
}
