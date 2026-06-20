using System.Net;
using Bearcat.Abstractions.DistributionSite;
using Bearcat.DistributionSites.BoerseCx.InversionOfControl;
using Bearcat.DistributionSites.DataLoadMe.InversionOfControl;
using Bearcat.DistributionSites.Shared.XenForo.Api;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.DistributionSites.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddDistributionSites()
        {
            services
                .AddHttpClient(XenForoForumClient.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() =>
                    new SocketsHttpHandler
                    {
                        UseCookies = false,
                        AutomaticDecompression = DecompressionMethods.All,
                        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                    }
                );

            services.AddBoerseCx();
            services.AddDataLoadMe();

            services.AddScoped<IDistributionSiteFactory, DistributionSiteFactory>();
        }
    }
}
