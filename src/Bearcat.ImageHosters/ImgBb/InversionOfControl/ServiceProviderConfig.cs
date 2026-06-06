using Bearcat.Abstractions.ImageHoster;
using Bearcat.ImageHosters.ImgBb.Api;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.ImageHosters.ImgBb.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddImgBb()
        {
            services.AddHttpClient<IImgBbApiClient, ImgBbApiClient>(client =>
            {
                client.BaseAddress = new Uri("https://api.imgbb.com");
                client.Timeout = Timeout.InfiniteTimeSpan;
            });

            services.AddKeyedScoped<IImageHoster, ImgBb>(nameof(ImgBb));
        }
    }
}
