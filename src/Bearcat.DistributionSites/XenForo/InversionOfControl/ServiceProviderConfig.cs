using Bearcat.Abstractions.DistributionSite;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.DistributionSites.XenForo.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddXenForo()
        {
            services.AddKeyedScoped<IDistributionSite, XenForo>(nameof(XenForo));
        }
    }
}
