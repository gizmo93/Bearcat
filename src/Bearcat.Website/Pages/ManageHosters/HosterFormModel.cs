using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageHosters;

public class HosterFormModel
{
    public string FullClassName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public Dictionary<string, string> Configuration { get; set; } = new();

    public bool IsEdit { get; set; }

    public bool HasUnreadableSecrets { get; set; }

    public int? HosterRegistrationId { get; set; }

    public int? MaxParallelUploadsOverride { get; set; }

    public string? UploadSpeedLimitMegabytesPerSecondInput { get; set; }

    public int? NumberOfHoursUntilReuploadOverride { get; set; }

    public ReuploadTrigger? ReuploadTriggerOverride { get; set; }

    public bool AlwaysReuploadAllFiles { get; set; }

    public bool UseForMirrorDownloads { get; set; }

    public int MirrorPriority { get; set; } = 100;

    public string? MirrorDownloadSpeedLimitMegabytesPerSecondInput { get; set; }

    public ProxySelection UploadProxySelection { get; set; } = ProxySelection.UseCategoryDefault;

    public int? UploadProxyServerId { get; set; }

    public ProxySelection MirrorDownloadProxySelection { get; set; } =
        ProxySelection.UseCategoryDefault;

    public int? MirrorDownloadProxyServerId { get; set; }
}
