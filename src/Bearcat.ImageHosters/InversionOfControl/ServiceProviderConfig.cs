using Bearcat.Abstractions.ImageHoster;
using Bearcat.Abstractions.Proxies;
using Bearcat.ImageHosters.DirectUpload.InversionOfControl;
using Bearcat.ImageHosters.ImgBb.InversionOfControl;
using Bearcat.ImageHosters.LoePic.InversionOfControl;
using Bearcat.ImageHosters.PixelFox.InversionOfControl;
using Bearcat.ImageHosters.PixHost.InversionOfControl;
using Bearcat.ImageHosters.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.ImageHosters.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddImageHosters()
        {
            services
                .AddHttpClient<ImageUrlExistenceChecker>(client =>
                    client.Timeout = TimeSpan.FromSeconds(30)
                )
                .UseProxyForCategory(ProxyCategory.ImageHosters);

            services.AddImgBb();
            services.AddPixHost();
            services.AddPixelFox();
            services.AddLoePic();
            services.AddDirectUpload();
            services.AddScoped<IImageHosterFactory, ImageHosterFactory>();
        }
    }
}
