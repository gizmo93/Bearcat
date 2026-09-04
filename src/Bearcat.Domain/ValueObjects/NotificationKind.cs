namespace Bearcat.Domain.ValueObjects;

public enum NotificationKind
{
    Legacy = 1,
    ReleaseAutomaticallyCreated = 2,
    ReleaseFolderMissing = 3,
    ArchiveCreationFailed = 4,
    ArchiveFilesMissing = 5,
    InitialUploadCreated = 6,
    UploadCompleted = 7,
    UploadFailed = 8,
    UploadCancellationRequested = 9,
    UploadCanceled = 10,
    FilesOffline = 11,
    UploadMarkedOffline = 12,
    HosterStatusCheckFailed = 13,
    AutomaticReuploadCreated = 14,
    CaptchaVerificationRequired = 15,
    LinkCrypterContainerCreationFailed = 16,
    LinkCrypterContainerUpdateFailed = 17,
    CollectionLinkCrypterContainerInvalid = 18,
}
