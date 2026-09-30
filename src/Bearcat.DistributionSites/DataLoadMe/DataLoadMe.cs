using Bearcat.Abstractions.DistributionSite;
using Bearcat.DistributionSites.Shared.XenForo;

namespace Bearcat.DistributionSites.DataLoadMe;

public sealed class DataLoadMe(IHttpClientFactory httpClientFactory)
    : XenForoDistributionSiteBase<DataLoadMeConfig>(httpClientFactory)
{
    public override string Name => "data-load.me";

    public override string GetBaseUrl(IDistributionSiteConfig config) =>
        "https://www.data-load.me/";
}
