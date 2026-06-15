using Bearcat.Abstractions.ImageHoster;
using Bearcat.ImageHosters.ImgBb.InversionOfControl;
using Bearcat.ImageHosters.PixelFox.InversionOfControl;
using Bearcat.ImageHosters.PixHost.InversionOfControl;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.ImageHosters.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddImageHosters()
        {
            services.AddImgBb();
            services.AddPixHost();
            services.AddPixelFox();
            services.AddScoped<IImageHosterFactory, ImageHosterFactory>();
        }
    }
}
