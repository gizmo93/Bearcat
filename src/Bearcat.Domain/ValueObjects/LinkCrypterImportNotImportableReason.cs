namespace Bearcat.Domain.ValueObjects;

public enum LinkCrypterImportNotImportableReason
{
    ReleaseAlreadyExists = 1,
    SameNameAsEarlierContainer = 2,
    NoMirrorForReleaseTemplateHoster = 3,
    NoAdoptedMirrorAndNoReleaseFolder = 4,
    ArchiveFilesBasePathMissing = 5,
}
