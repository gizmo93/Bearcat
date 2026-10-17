using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Proxies;
using Bearcat.Hosters.DoodStream.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.Hosters.DoodStream.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddDoodStream()
        {
            services
                .AddRefitClient<IDoodStreamApi>(
                    new RefitSettings
                    {
                        ContentSerializer = new SystemTextJsonContentSerializer(
                            jsonSerializerOptions: new JsonSerializerOptions
                            {
                                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                                PropertyNameCaseInsensitive = true,
                            }
                        ),
                    }
                )
                .ConfigureHttpClient(c =>
                {
                    c.BaseAddress = new Uri(ApiClient.ApiBaseUrl);
                    c.Timeout = Timeout.InfiniteTimeSpan;
                })
                .UseProxyForCategory(ProxyCategory.HosterUploads);

            services.AddScoped<ApiClient>();
            services.AddKeyedScoped<IHoster, DoodStream>(nameof(DoodStream));
        }
    }
}
