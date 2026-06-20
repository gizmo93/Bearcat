using Bearcat.Abstractions.DistributionSite;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.DistributionSites.DataLoadMe.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddDataLoadMe()
        {
            services.AddKeyedScoped<IDistributionSite, DataLoadMe>(nameof(DataLoadMe));
        }
    }
}
