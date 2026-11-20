namespace Bearcat.Domain.ValueObjects;

public enum LinkCrypterImportState
{
    ReadingContainers = 1,
    WaitingForHosterRegistrations = 2,
    CheckingFiles = 3,
    ReadyForReview = 4,
    Importing = 5,
    Completed = 6,
    Failed = 7,
}
