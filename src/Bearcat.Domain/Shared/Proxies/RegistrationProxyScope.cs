using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.Entities;

namespace Bearcat.Domain.Shared.Proxies;

public static class RegistrationProxyScope
{
    public static IDisposable EnterForHosterUpload(HosterRegistration registration)
    {
        return EnterForHosterUpload(
            registration.UploadProxySelection,
            registration.UploadProxyServerId
        );
    }

    public static IDisposable EnterForHosterUpload(
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

    public static IDisposable EnterForHosterMirrorDownload(HosterRegistration registration)
    {
        return ProxyCategoryScope.Enter(
            ProxyCategory.HosterMirrorDownloads,
            registration.MirrorDownloadProxySelection,
            registration.MirrorDownloadProxyServerId
        );
    }

    public static IDisposable EnterForImageHoster(ImageHosterRegistration registration)
    {
        return ProxyCategoryScope.Enter(
            ProxyCategory.ImageHosters,
            registration.ProxySelection,
            registration.ProxyServerId
        );
    }

    public static IDisposable EnterForLinkCrypter(LinkCrypterRegistration registration)
    {
        return ProxyCategoryScope.Enter(
            ProxyCategory.LinkCrypters,
            registration.ProxySelection,
            registration.ProxyServerId
        );
    }
}
