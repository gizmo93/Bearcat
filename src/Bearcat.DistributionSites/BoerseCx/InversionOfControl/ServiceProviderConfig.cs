using Bearcat.Abstractions.DistributionSite;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.DistributionSites.BoerseCx.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddBoerseCx()
        {
            services.AddKeyedScoped<IDistributionSite, BoerseCx>(nameof(BoerseCx));
        }
    }
}
