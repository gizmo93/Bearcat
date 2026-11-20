using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Proxies;
using Bearcat.Hosters.Zippyshare.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.Hosters.Zippyshare.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddZippyshare()
        {
            services
                .AddRefitClient<IZippyshareApi>(
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
                    c.BaseAddress = new Uri(ApiClient.BaseUrl);
                    c.Timeout = Timeout.InfiniteTimeSpan;
                })
                .UseProxyForCategory(ProxyCategory.HosterUploads);

            services.AddScoped<IZippyshareApiClient, ApiClient>();
            services.AddKeyedScoped<IHoster, Zippyshare>(nameof(Zippyshare));
        }
    }
}
