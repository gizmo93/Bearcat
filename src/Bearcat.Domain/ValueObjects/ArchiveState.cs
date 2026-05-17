namespace Bearcat.Domain.ValueObjects;

public enum ArchiveState
{
    Creating = 1,
    Created = 2,
    CreationFailed = 3,
    MissingFiles = 4,
    Deleted = 5,
}
