using Bearcat.Abstractions.ImageHoster;
using Bearcat.ImageHosters.PixelFox.Api;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.ImageHosters.PixelFox.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddPixelFox()
        {
            services.AddHttpClient<IPixelFoxApiClient, PixelFoxApiClient>(client =>
            {
                client.BaseAddress = new Uri(PixelFoxApiClient.BaseUrl);
                client.Timeout = Timeout.InfiniteTimeSpan;
            });

            services.AddKeyedScoped<IImageHoster, PixelFox>(nameof(PixelFox));
        }
    }
}
