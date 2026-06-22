using Bearcat.Abstractions.Media;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Media.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddMedia()
        {
            services.AddScoped<IMediaMetadataExtractor, MediaInfoMetadataExtractor>();
        }
    }
}
