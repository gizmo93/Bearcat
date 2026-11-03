namespace Bearcat.Domain.ValueObjects;

public enum LinkCrypterImportNotImportableReason
{
    NoMirrorForReleaseHoster = 1,
    NoAdoptedMirrorAndNoReleaseFolder = 2,
    ArchiveFilesBasePathMissing = 3,
}
