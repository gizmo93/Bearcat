using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.Shared.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class HosterRegistration : IEntityWithEncryptedSecrets, IActivatableEntity
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string SerializedConfig { get; set; }

    public bool IsActive { get; set; }

    public bool HasUnreadableSecrets { get; set; }

    public bool RequiresCaptchaVerification { get; set; }

    public required string HosterClassName { get; set; }

    public int? MaxParallelUploadsOverride { get; set; }

    public int? NumberOfHoursUntilReuploadOverride { get; set; }

    public ReuploadTrigger? ReuploadTriggerOverride { get; set; }

    public bool AlwaysReuploadAllFiles { get; set; }

    public bool UseForMirrorDownloads { get; set; }

    public int MirrorPriority { get; set; } = 100;

    public ProxySelection UploadProxySelection { get; set; } = ProxySelection.UseCategoryDefault;

    public int? UploadProxyServerId { get; set; }

    public ProxySelection MirrorDownloadProxySelection { get; set; } =
        ProxySelection.UseCategoryDefault;

    public int? MirrorDownloadProxyServerId { get; set; }

    public List<UploadConfig> UploadConfigs { get; set; } = null!;

    public string GetEncryptedSecrets() => SerializedConfig;

    public string GetRegistrationTypeName() => "Hoster";

    public string? GetRegistrationName() => Name;
}
