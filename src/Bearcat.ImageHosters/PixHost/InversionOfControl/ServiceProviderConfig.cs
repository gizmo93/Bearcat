using Bearcat.Abstractions.ImageHoster;
using Bearcat.Abstractions.Proxies;
using Bearcat.ImageHosters.PixHost.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.ImageHosters.PixHost.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddPixHost()
        {
            services
                .AddRefitClient<IPixHostApi>()
                .ConfigureHttpClient(client =>
                {
                    client.BaseAddress = new Uri(PixHostApiClient.ApiBaseUrl);
                    client.Timeout = Timeout.InfiniteTimeSpan;
                })
                .UseProxyForCategory(ProxyCategory.ImageHosters);

            services
                .AddHttpClient<IPixHostApiClient, PixHostApiClient>(client =>
                {
                    client.Timeout = Timeout.InfiniteTimeSpan;
                })
                .UseProxyForCategory(ProxyCategory.ImageHosters);

            services.AddKeyedScoped<IImageHoster, PixHost>(nameof(PixHost));
        }
    }
}
