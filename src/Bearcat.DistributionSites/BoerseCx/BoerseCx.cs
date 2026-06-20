using Bearcat.DistributionSites.Shared.XenForo;

namespace Bearcat.DistributionSites.BoerseCx;

public sealed class BoerseCx(IHttpClientFactory httpClientFactory)
    : XenForoDistributionSiteBase<BoerseCxConfig>(httpClientFactory)
{
    public override string Name => "boerse.cx";

    protected override string BaseUrl => "https://boerse.cx/";
}
