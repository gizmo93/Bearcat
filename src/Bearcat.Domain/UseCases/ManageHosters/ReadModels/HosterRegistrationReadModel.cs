using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageHosters.ReadModels;

public record HosterRegistrationReadModel(
    int Id,
    string Name,
    bool IsActive,
    bool HasUnreadableSecrets,
    bool RequiresCaptchaVerification,
    bool SupportsCaptchaVerification,
    bool SupportsPremiumOnlyDownloads,
    int? MaxFileSizeMb,
    bool HasFixedParallelUploadLimit,
    int? DefaultMaximumParallelUploads,
    int? MaxParallelUploadsOverride,
    decimal? UploadSpeedLimitMegabytesPerSecond,
    int? NumberOfHoursUntilReuploadOverride,
    ReuploadTrigger? ReuploadTriggerOverride,
    bool AlwaysReuploadAllFiles,
    bool SupportsDownload,
    bool DownloadRequiresPremium,
    bool UseForMirrorDownloads,
    int MirrorPriority,
    decimal? MirrorDownloadSpeedLimitMegabytesPerSecond,
    string HosterName,
    string FullClassName,
    ProxySelection UploadProxySelection,
    int? UploadProxyServerId,
    string? UploadProxyServerName,
    ProxySelection MirrorDownloadProxySelection,
    int? MirrorDownloadProxyServerId,
    string? MirrorDownloadProxyServerName
);
