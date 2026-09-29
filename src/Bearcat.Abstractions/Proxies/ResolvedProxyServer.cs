using System.Net;

namespace Bearcat.Abstractions.Proxies;

public record ResolvedProxyServer(
    int Id,
    string Name,
    Uri ProxyUri,
    NetworkCredential? Credential,
    bool HasUnreadableSecrets
);
