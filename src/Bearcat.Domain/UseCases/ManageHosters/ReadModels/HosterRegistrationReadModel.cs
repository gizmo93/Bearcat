using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageHosters.ReadModels;

public record HosterRegistrationReadModel(
    int Id,
    string Name,
    bool IsActive,
    bool RequiresCaptchaVerification,
    bool SupportsCaptchaVerification,
    bool SupportsPremiumOnlyDownloads,
    int? MaxFileSizeMb,
    bool HasFixedParallelUploadLimit,
    int? DefaultMaximumParallelUploads,
    int? MaxParallelUploadsOverride,
    int? NumberOfHoursUntilReuploadOverride,
    ReuploadTrigger? ReuploadTriggerOverride,
    bool AlwaysReuploadAllFiles,
    bool SupportsDownload,
    bool DownloadRequiresPremium,
    bool UseForMirrorDownloads,
    int MirrorPriority,
    string HosterName,
    string FullClassName
);
