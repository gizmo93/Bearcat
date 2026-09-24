using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageNotifications;

public sealed record NotificationDefinition(
    NotificationKind Kind,
    NotificationSeverity Severity,
    NotificationGroup Group,
    string DisplayName,
    string Description
);

public static class NotificationDefinitions
{
    public static IReadOnlyList<NotificationDefinition> All { get; } =
    [
        new(
            Kind: NotificationKind.ReleaseAutomaticallyCreated,
            Severity: NotificationSeverity.Info,
            Group: NotificationGroup.Releases,
            DisplayName: "NotificationKind.ReleaseAutomaticallyCreated",
            Description: "NotificationKind.ReleaseAutomaticallyCreated.Description"
        ),
        new(
            Kind: NotificationKind.ReleaseAutoConvertedToUnmanaged,
            Severity: NotificationSeverity.Info,
            Group: NotificationGroup.Releases,
            DisplayName: "NotificationKind.ReleaseAutoConvertedToUnmanaged",
            Description: "NotificationKind.ReleaseAutoConvertedToUnmanaged.Description"
        ),
        new(
            Kind: NotificationKind.ReleaseFolderMissing,
            Severity: NotificationSeverity.Error,
            Group: NotificationGroup.Releases,
            DisplayName: "NotificationKind.ReleaseFolderMissing",
            Description: "NotificationKind.ReleaseFolderMissing.Description"
        ),
        new(
            Kind: NotificationKind.ArchiveCreationFailed,
            Severity: NotificationSeverity.Error,
            Group: NotificationGroup.Archives,
            DisplayName: "NotificationKind.ArchiveCreationFailed",
            Description: "NotificationKind.ArchiveCreationFailed.Description"
        ),
        new(
            Kind: NotificationKind.ArchiveFilesMissing,
            Severity: NotificationSeverity.Warning,
            Group: NotificationGroup.Archives,
            DisplayName: "NotificationKind.ArchiveFilesMissing",
            Description: "NotificationKind.ArchiveFilesMissing.Description"
        ),
        new(
            Kind: NotificationKind.ArchiveRestoreFailed,
            Severity: NotificationSeverity.Error,
            Group: NotificationGroup.Archives,
            DisplayName: "NotificationKind.ArchiveRestoreFailed",
            Description: "NotificationKind.ArchiveRestoreFailed.Description"
        ),
        new(
            Kind: NotificationKind.InitialUploadCreated,
            Severity: NotificationSeverity.Info,
            Group: NotificationGroup.Uploads,
            DisplayName: "NotificationKind.InitialUploadCreated",
            Description: "NotificationKind.InitialUploadCreated.Description"
        ),
        new(
            Kind: NotificationKind.UploadCompleted,
            Severity: NotificationSeverity.Info,
            Group: NotificationGroup.Uploads,
            DisplayName: "NotificationKind.UploadCompleted",
            Description: "NotificationKind.UploadCompleted.Description"
        ),
        new(
            Kind: NotificationKind.UploadFailed,
            Severity: NotificationSeverity.Error,
            Group: NotificationGroup.Uploads,
            DisplayName: "NotificationKind.UploadFailed",
            Description: "NotificationKind.UploadFailed.Description"
        ),
        new(
            Kind: NotificationKind.UploadCancellationRequested,
            Severity: NotificationSeverity.Info,
            Group: NotificationGroup.Uploads,
            DisplayName: "NotificationKind.UploadCancellationRequested",
            Description: "NotificationKind.UploadCancellationRequested.Description"
        ),
        new(
            Kind: NotificationKind.UploadCanceled,
            Severity: NotificationSeverity.Info,
            Group: NotificationGroup.Uploads,
            DisplayName: "NotificationKind.UploadCanceled",
            Description: "NotificationKind.UploadCanceled.Description"
        ),
        new(
            Kind: NotificationKind.FilesOffline,
            Severity: NotificationSeverity.Warning,
            Group: NotificationGroup.Availability,
            DisplayName: "NotificationKind.FilesOffline",
            Description: "NotificationKind.FilesOffline.Description"
        ),
        new(
            Kind: NotificationKind.UploadMarkedOffline,
            Severity: NotificationSeverity.Warning,
            Group: NotificationGroup.Availability,
            DisplayName: "NotificationKind.UploadMarkedOffline",
            Description: "NotificationKind.UploadMarkedOffline.Description"
        ),
        new(
            Kind: NotificationKind.HosterStatusCheckFailed,
            Severity: NotificationSeverity.Error,
            Group: NotificationGroup.Availability,
            DisplayName: "NotificationKind.HosterStatusCheckFailed",
            Description: "NotificationKind.HosterStatusCheckFailed.Description"
        ),
        new(
            Kind: NotificationKind.AutomaticReuploadCreated,
            Severity: NotificationSeverity.Info,
            Group: NotificationGroup.Uploads,
            DisplayName: "NotificationKind.AutomaticReuploadCreated",
            Description: "NotificationKind.AutomaticReuploadCreated.Description"
        ),
        new(
            Kind: NotificationKind.CaptchaVerificationRequired,
            Severity: NotificationSeverity.Warning,
            Group: NotificationGroup.Hosters,
            DisplayName: "NotificationKind.CaptchaVerificationRequired",
            Description: "NotificationKind.CaptchaVerificationRequired.Description"
        ),
        new(
            Kind: NotificationKind.LinkCrypterContainerCreationFailed,
            Severity: NotificationSeverity.Error,
            Group: NotificationGroup.LinkCrypters,
            DisplayName: "NotificationKind.LinkCrypterContainerCreationFailed",
            Description: "NotificationKind.LinkCrypterContainerCreationFailed.Description"
        ),
        new(
            Kind: NotificationKind.LinkCrypterContainerUpdateFailed,
            Severity: NotificationSeverity.Error,
            Group: NotificationGroup.LinkCrypters,
            DisplayName: "NotificationKind.LinkCrypterContainerUpdateFailed",
            Description: "NotificationKind.LinkCrypterContainerUpdateFailed.Description"
        ),
        new(
            Kind: NotificationKind.CollectionLinkCrypterContainerInvalid,
            Severity: NotificationSeverity.Error,
            Group: NotificationGroup.LinkCrypters,
            DisplayName: "NotificationKind.CollectionLinkCrypterContainerInvalid",
            Description: "NotificationKind.CollectionLinkCrypterContainerInvalid.Description"
        ),
        new(
            Kind: NotificationKind.AutomaticForumPostCreated,
            Severity: NotificationSeverity.Info,
            Group: NotificationGroup.DistributionSites,
            DisplayName: "NotificationKind.AutomaticForumPostCreated",
            Description: "NotificationKind.AutomaticForumPostCreated.Description"
        ),
        new(
            Kind: NotificationKind.AutomaticForumPostFailed,
            Severity: NotificationSeverity.Error,
            Group: NotificationGroup.DistributionSites,
            DisplayName: "NotificationKind.AutomaticForumPostFailed",
            Description: "NotificationKind.AutomaticForumPostFailed.Description"
        ),
        new(
            Kind: NotificationKind.AutomaticForumPostUpdated,
            Severity: NotificationSeverity.Info,
            Group: NotificationGroup.DistributionSites,
            DisplayName: "NotificationKind.AutomaticForumPostUpdated",
            Description: "NotificationKind.AutomaticForumPostUpdated.Description"
        ),
        new(
            Kind: NotificationKind.AutomaticForumPostUpdateFailed,
            Severity: NotificationSeverity.Error,
            Group: NotificationGroup.DistributionSites,
            DisplayName: "NotificationKind.AutomaticForumPostUpdateFailed",
            Description: "NotificationKind.AutomaticForumPostUpdateFailed.Description"
        ),
        new(
            Kind: NotificationKind.ReleaseCreatedFromRemoteDownload,
            Severity: NotificationSeverity.Info,
            Group: NotificationGroup.RemoteDownloads,
            DisplayName: "NotificationKind.ReleaseCreatedFromRemoteDownload",
            Description: "NotificationKind.ReleaseCreatedFromRemoteDownload.Description"
        ),
        new(
            Kind: NotificationKind.RemoteDownloadFailed,
            Severity: NotificationSeverity.Error,
            Group: NotificationGroup.RemoteDownloads,
            DisplayName: "NotificationKind.RemoteDownloadFailed",
            Description: "NotificationKind.RemoteDownloadFailed.Description"
        ),
    ];

    public static NotificationDefinition Get(NotificationKind kind)
    {
        return All.FirstOrDefault(definition => definition.Kind == kind)
            ?? throw new ArgumentOutOfRangeException(
                paramName: nameof(kind),
                actualValue: kind,
                message: "Notifications must use a defined notification kind."
            );
    }
}
