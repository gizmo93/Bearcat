using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.Entities;

namespace Bearcat.Domain.Shared.Proxies;

public static class LinkCrypterRegistrationProxyScope
{
    public static IDisposable Enter(LinkCrypterRegistration registration)
    {
        return ProxyCategoryScope.Enter(
            ProxyCategory.LinkCrypters,
            registration.ProxySelection,
            registration.ProxyServerId
        );
    }
}
