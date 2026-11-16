namespace Bearcat.Domain.UseCases.ManageArchives.ArchiveFolderRelocations;

public enum ArchiveFolderNotRelocatableReason
{
    ArchiveIsNotDeletedOrMissingFiles = 1,
    NewerArchiveExistsInArchiveConfig = 2,
    NoMatchingFolderFound = 3,
    SeveralMatchingFoldersFound = 4,
    MatchingFolderContainsOtherArchive = 5,
    MatchingFolderUsedByCreatedArchive = 6,
    MatchingFolderFoundForSeveralSelectedArchives = 7,
    MatchingFolderInsideReleaseFolderOfManagedRelease = 8,
}
