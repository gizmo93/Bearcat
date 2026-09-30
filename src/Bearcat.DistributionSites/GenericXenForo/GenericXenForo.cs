using Bearcat.Abstractions.ConfigurationFields;
using Bearcat.Abstractions.DistributionSite;
using Bearcat.DistributionSites.Extensions;
using Bearcat.DistributionSites.Shared.XenForo;

namespace Bearcat.DistributionSites.GenericXenForo;

public sealed class GenericXenForo(IHttpClientFactory httpClientFactory)
    : XenForoDistributionSiteBase<GenericXenForoConfig>(httpClientFactory)
{
    public override string Name => "XenForo";

    public override IReadOnlyList<ConfigurationField> ConfigurationFields =>
        [
            new(nameof(GenericXenForoConfig.BaseUrl), ConfigurationFieldType.Url, IsRequired: true),
            .. base.ConfigurationFields,
        ];

    public override string GetBaseUrl(IDistributionSiteConfig config) =>
        config.As<GenericXenForoConfig>().BaseUrl;
}
