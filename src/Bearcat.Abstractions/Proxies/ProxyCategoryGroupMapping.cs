namespace Bearcat.Abstractions.Proxies;

public static class ProxyCategoryGroupMapping
{
    public static ProxyCategoryGroup GetGroup(ProxyCategory category)
    {
        return category switch
        {
            ProxyCategory.HosterUploads => ProxyCategoryGroup.Hosters,
            ProxyCategory.HosterMirrorDownloads => ProxyCategoryGroup.Hosters,
            ProxyCategory.ImageHosters => ProxyCategoryGroup.ImageHosters,
            ProxyCategory.LinkCrypters => ProxyCategoryGroup.LinkCrypters,
            ProxyCategory.NfoDatabases => ProxyCategoryGroup.NfoDatabases,
            ProxyCategory.MediaDatabases => ProxyCategoryGroup.MediaDatabases,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
        };
    }
}
