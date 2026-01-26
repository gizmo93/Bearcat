using System.Text.Json;
using System.Text.Json.Serialization;
using BearCat.Core.Domain.Abstractions.Hoster;
using BearCat.Core.Infrastructure.Hosters.DDownload.ApiClient;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace BearCat.Core.Infrastructure.Hosters.DDownload.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddDdownload()
        {
            services.AddRefitClient<IDDownloadApi>(
                    new RefitSettings
                    {
                        ContentSerializer = new SystemTextJsonContentSerializer(
                            jsonSerializerOptions: new JsonSerializerOptions
                            {
                                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                                PropertyNameCaseInsensitive = true,
                            })
                    })
                .ConfigureHttpClient(c =>
                {
                    c.BaseAddress = new Uri(DDownloadApiClient.ApiBaseUrl);
                    c.Timeout = Timeout.InfiniteTimeSpan;
                });

            services.AddHttpClient(
                "DDownloadUpload",
                c =>
                {
                    c.Timeout = Timeout.InfiniteTimeSpan;
                });

            services.AddScoped<DDownloadApiClient>();
            services.AddKeyedScoped<IHoster, DDownload>(nameof(DDownload));
        }
    }
}
