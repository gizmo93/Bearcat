using Bearcat.Abstractions.ImageHoster;
using Bearcat.ImageHosters.DirectUpload.Api;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.ImageHosters.DirectUpload.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddDirectUpload()
        {
            services.AddHttpClient<IDirectUploadApiClient, DirectUploadApiClient>(client =>
            {
                client.BaseAddress = new Uri(DirectUploadApiClient.BaseUrl);
                client.Timeout = Timeout.InfiniteTimeSpan;
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) "
                        + "AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.0 Safari/605.1.15"
                );
            });

            services.AddKeyedScoped<IImageHoster, DirectUpload>(nameof(DirectUpload));
        }
    }
}
