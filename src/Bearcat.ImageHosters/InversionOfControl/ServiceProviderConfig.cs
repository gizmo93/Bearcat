using Bearcat.Abstractions.ImageHoster;
using Bearcat.ImageHosters.ImgBb.InversionOfControl;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.ImageHosters.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddImageHosters()
        {
            services.AddImgBb();
            services.AddScoped<IImageHosterFactory, ImageHosterFactory>();
        }
    }
}
