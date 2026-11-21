using System.Net;
using Bearcat.Abstractions.DistributionSite;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.DistributionSites.WordPress.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddWordPress()
        {
            services
                .AddHttpClient(WordPress.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() =>
                    new SocketsHttpHandler
                    {
                        UseCookies = false,
                        AllowAutoRedirect = false,
                        AutomaticDecompression = DecompressionMethods.All,
                        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                    }
                );

            services.AddKeyedScoped<IDistributionSite, WordPress>(nameof(WordPress));
        }
    }
}
