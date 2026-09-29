using Bearcat.Abstractions.ImageHoster;
using Bearcat.Abstractions.Proxies;
using Bearcat.ImageHosters.LoePic.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.ImageHosters.LoePic.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddLoePic()
        {
            services
                .AddRefitClient<ILoePicApi>()
                .ConfigureHttpClient(client =>
                {
                    client.BaseAddress = new Uri(LoePicApiClient.BaseUrl);
                    client.Timeout = Timeout.InfiniteTimeSpan;
                })
                .UseProxyForCategory(ProxyCategory.ImageHosters);

            services.AddScoped<ILoePicApiClient, LoePicApiClient>();
            services.AddKeyedScoped<IImageHoster, LoePic>(nameof(LoePic));
        }
    }
}
