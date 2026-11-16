using System.Net.Http.Headers;
using Bearcat.Abstractions.Media;
using Bearcat.Media.Ffmpeg;
using Bearcat.Media.Screenshots;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Media.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddMedia()
        {
            services.AddScoped<IMediaMetadataExtractor, MediaInfoMetadataExtractor>();

            services.AddHttpClient(
                FfmpegInstaller.HttpClientName,
                client =>
                {
                    client.Timeout = TimeSpan.FromMinutes(30);
                    client.DefaultRequestHeaders.UserAgent.Add(
                        new ProductInfoHeaderValue("Bearcat", "1.0")
                    );
                }
            );
            services.AddSingleton<IFfmpegInstaller, FfmpegInstaller>();
            services.AddSingleton<IVideoScreenshotCreator, FfmpegVideoScreenshotCreator>();
        }
    }
}
