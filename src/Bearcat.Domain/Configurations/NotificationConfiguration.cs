using Bearcat.Abstractions.Configurations;

namespace Bearcat.Domain.Configurations;

[ApplicationConfiguration(
    "Notifications",
    "NotificationSettings",
    "NotificationSettingsDescription"
)]
public class NotificationConfiguration : IApplicationConfiguration
{
    [ApplicationConfigurationProperty(
        "NotificationKind.ReleaseAutomaticallyCreated",
        "NotificationKind.ReleaseAutomaticallyCreated.Description"
    )]
    public bool ReleaseAutomaticallyCreated { get; set; } = true;

    [ApplicationConfigurationProperty(
        "NotificationKind.ReleaseFolderMissing",
        "NotificationKind.ReleaseFolderMissing.Description"
    )]
    public bool ReleaseFolderMissing { get; set; } = true;

    [ApplicationConfigurationProperty(
        "NotificationKind.ArchiveCreationFailed",
        "NotificationKind.ArchiveCreationFailed.Description"
    )]
    public bool ArchiveCreationFailed { get; set; } = true;

    [ApplicationConfigurationProperty(
        "NotificationKind.ArchiveFilesMissing",
        "NotificationKind.ArchiveFilesMissing.Description"
    )]
    public bool ArchiveFilesMissing { get; set; } = true;

    [ApplicationConfigurationProperty(
        "NotificationKind.InitialUploadCreated",
        "NotificationKind.InitialUploadCreated.Description"
    )]
    public bool InitialUploadCreated { get; set; } = true;

    [ApplicationConfigurationProperty(
        "NotificationKind.UploadCompleted",
        "NotificationKind.UploadCompleted.Description"
    )]
    public bool UploadCompleted { get; set; } = true;

    [ApplicationConfigurationProperty(
        "NotificationKind.UploadFailed",
        "NotificationKind.UploadFailed.Description"
    )]
    public bool UploadFailed { get; set; } = true;

    [ApplicationConfigurationProperty(
        "NotificationKind.UploadCancellationRequested",
        "NotificationKind.UploadCancellationRequested.Description"
    )]
    public bool UploadCancellationRequested { get; set; } = true;

    [ApplicationConfigurationProperty(
        "NotificationKind.UploadCanceled",
        "NotificationKind.UploadCanceled.Description"
    )]
    public bool UploadCanceled { get; set; } = true;

    [ApplicationConfigurationProperty(
        "NotificationKind.FilesOffline",
        "NotificationKind.FilesOffline.Description"
    )]
    public bool FilesOffline { get; set; } = true;

    [ApplicationConfigurationProperty(
        "NotificationKind.UploadMarkedOffline",
        "NotificationKind.UploadMarkedOffline.Description"
    )]
    public bool UploadMarkedOffline { get; set; } = true;

    [ApplicationConfigurationProperty(
        "NotificationKind.HosterStatusCheckFailed",
        "NotificationKind.HosterStatusCheckFailed.Description"
    )]
    public bool HosterStatusCheckFailed { get; set; } = true;

    [ApplicationConfigurationProperty(
        "NotificationKind.AutomaticReuploadCreated",
        "NotificationKind.AutomaticReuploadCreated.Description"
    )]
    public bool AutomaticReuploadCreated { get; set; } = true;

    [ApplicationConfigurationProperty(
        "NotificationKind.CaptchaVerificationRequired",
        "NotificationKind.CaptchaVerificationRequired.Description"
    )]
    public bool CaptchaVerificationRequired { get; set; } = true;

    [ApplicationConfigurationProperty(
        "NotificationKind.LinkCrypterContainerCreationFailed",
        "NotificationKind.LinkCrypterContainerCreationFailed.Description"
    )]
    public bool LinkCrypterContainerCreationFailed { get; set; } = true;

    [ApplicationConfigurationProperty(
        "NotificationKind.LinkCrypterContainerUpdateFailed",
        "NotificationKind.LinkCrypterContainerUpdateFailed.Description"
    )]
    public bool LinkCrypterContainerUpdateFailed { get; set; } = true;

    [ApplicationConfigurationProperty(
        "NotificationKind.CollectionLinkCrypterContainerInvalid",
        "NotificationKind.CollectionLinkCrypterContainerInvalid.Description"
    )]
    public bool CollectionLinkCrypterContainerInvalid { get; set; } = true;
}
