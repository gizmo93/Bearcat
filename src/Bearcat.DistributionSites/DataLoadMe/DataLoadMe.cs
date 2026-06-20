using Bearcat.DistributionSites.Shared.XenForo;

namespace Bearcat.DistributionSites.DataLoadMe;

public sealed class DataLoadMe(IHttpClientFactory httpClientFactory)
    : XenForoDistributionSiteBase<DataLoadMeConfig>(httpClientFactory)
{
    public override string Name => "data-load.me";

    protected override string BaseUrl => "https://www.data-load.me/";
}
