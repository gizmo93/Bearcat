using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.Entities;

namespace Bearcat.Domain.Shared.Proxies;

public static class ImageHosterRegistrationProxyScope
{
    public static IDisposable Enter(ImageHosterRegistration registration)
    {
        return ProxyCategoryScope.Enter(
            ProxyCategory.ImageHosters,
            registration.ProxySelection,
            registration.ProxyServerId
        );
    }
}
