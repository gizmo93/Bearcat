using Bearcat.DistributionSites.Shared.XenForo;

namespace Bearcat.DistributionSites.BoerseCx;

public sealed class BoerseCx(IHttpClientFactory httpClientFactory)
    : XenForoDistributionSiteBase<BoerseCxConfig>(httpClientFactory)
{
    public override string Name => "boerse.cx";

    public override string BaseUrl => "https://boerse.cx/";
}
