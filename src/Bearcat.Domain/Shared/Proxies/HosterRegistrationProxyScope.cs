using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.Entities;

namespace Bearcat.Domain.Shared.Proxies;

public static class HosterRegistrationProxyScope
{
    public static IDisposable EnterForUpload(HosterRegistration registration)
    {
        return EnterForUpload(registration.UploadProxySelection, registration.UploadProxyServerId);
    }

    public static IDisposable EnterForUpload(
        ProxySelection uploadProxySelection,
        int? uploadProxyServerId
    )
    {
        return ProxyCategoryScope.Enter(
            ProxyCategory.HosterUploads,
            uploadProxySelection,
            uploadProxyServerId
        );
    }

    public static IDisposable EnterForMirrorDownload(HosterRegistration registration)
    {
        return ProxyCategoryScope.Enter(
            ProxyCategory.HosterMirrorDownloads,
            registration.MirrorDownloadProxySelection,
            registration.MirrorDownloadProxyServerId
        );
    }
}
