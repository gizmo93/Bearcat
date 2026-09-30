using Bearcat.Abstractions.DistributionSite;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.DistributionSites.GenericXenForo.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddGenericXenForo()
        {
            services.AddKeyedScoped<IDistributionSite, GenericXenForo>(nameof(GenericXenForo));
        }
    }
}
