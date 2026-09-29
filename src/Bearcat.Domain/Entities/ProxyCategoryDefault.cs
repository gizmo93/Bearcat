using Bearcat.Abstractions.Proxies;

namespace Bearcat.Domain.Entities;

public class ProxyCategoryDefault
{
    public ProxyCategory ProxyCategory { get; set; }

    public int? ProxyServerId { get; set; }

    public ProxyServer? ProxyServer { get; set; }
}
