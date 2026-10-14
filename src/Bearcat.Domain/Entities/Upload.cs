using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class Upload
{
    public int Id { get; set; }

    public int UploadConfigId { get; set; }

    public UploadConfig UploadConfig { get; set; } = null!;

    public int? HosterRegistrationId { get; set; }

    public HosterRegistration? HosterRegistration { get; set; }

    public int? ArchiveId { get; set; }

    public Archive? Archive { get; set; }

    public string? HosterFolderId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UploadedAt { get; set; }

    public UploadState UploadState { get; set; }

    public OnlineState OnlineState { get; set; }

    public DateTime? NotFullyOnlineSince { get; set; }

    public DateTime? FullyOfflineSince { get; set; }

    public bool PremiumOnlyDownload { get; set; }

    public List<UploadedFile> UploadedFiles { get; set; } = null!;

    public DateTime? OldestUploadedFileCheckedAt { get; private set; }

    public List<string> ErrorMessages { get; set; } = [];

    public List<LinkCrypterContainer> LinkCrypterContainers { get; set; } = null!;

    public List<Notification> Notifications { get; set; } = null!;

    public string GetHosterClassName() =>
        HosterRegistrationId is null
            ? UploadConfig.GetHosterClassName()
            : HosterRegistration!.HosterClassName;

    public void SetOnlineStateAndOfflineSinceTimestamps(OnlineState onlineState, DateTime localNow)
    {
        OnlineState = onlineState;

        if (onlineState is OnlineState.PartiallyOnline or OnlineState.Offline)
        {
            NotFullyOnlineSince ??= localNow;
        }
        else
        {
            NotFullyOnlineSince = null;
        }

        if (onlineState is OnlineState.Offline)
        {
            FullyOfflineSince ??= localNow;
        }
        else
        {
            FullyOfflineSince = null;
        }
    }

    public OnlineState CalculateOnlineStateFromUploadedFiles()
    {
        var offlineFilesCount = UploadedFiles.Count(f => f.OnlineState == OnlineState.Offline);

        return offlineFilesCount switch
        {
            0 => OnlineState.Online,
            _ when offlineFilesCount == UploadedFiles.Count => OnlineState.Offline,
            _ => OnlineState.PartiallyOnline,
        };
    }

    public bool CanBeDeleted() =>
        UploadState
            is UploadState.Pending
                or UploadState.Completed
                or UploadState.Failed
                or UploadState.Canceled;

    public bool CanBeManuallyReuploaded() =>
        UploadState is UploadState.Canceled or UploadState.Failed
        || OnlineState is OnlineState.Offline or OnlineState.PartiallyOnline;

    public bool HasExistingOrPendingReplacementUpload() =>
        UploadConfig.Uploads.Any(otherUpload =>
            otherUpload.Id != Id
            && (
                otherUpload.OnlineState == OnlineState.Online
                || otherUpload.UploadState
                    is UploadState.Pending
                        or UploadState.Uploading
                        or UploadState.WaitingForArchive
                        or UploadState.Failed
                        or UploadState.CancellationRequested
            )
        );

    public List<ArchiveFile> GetArchiveFilesWithoutUploadedFile()
    {
        var processedArchiveFileIds = UploadedFiles.Select(uf => uf.ArchiveFileId).ToHashSet();

        return Archive!.ArchiveFiles.Where(af => !processedArchiveFileIds.Contains(af.Id)).ToList();
    }

    public bool HasHosterRegistrationAssigned() => HosterRegistrationId is not null;

    public bool IsHosterAccountPoolUploadWithoutUploadedFiles() =>
        UploadConfig.HosterAccountPoolId is not null && UploadedFiles.Count == 0;

    public bool ShouldSelectAnotherAccountOfHosterAccountPool() =>
        HasHosterRegistrationAssigned()
        && IsHosterAccountPoolUploadWithoutUploadedFiles()
        && !HosterRegistration!.IsUsableForUploads();

    public void RemoveHosterRegistrationAndFolderAssignment()
    {
        HosterRegistrationId = null;
        HosterRegistration = null;
        HosterFolderId = null;
    }
}
