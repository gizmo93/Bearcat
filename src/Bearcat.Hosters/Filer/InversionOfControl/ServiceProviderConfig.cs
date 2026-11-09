using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Proxies;
using Bearcat.Hosters.Filer.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.Hosters.Filer.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddFiler()
        {
            services
                .AddRefitClient<IFilerApi>(
                    new RefitSettings
                    {
                        ContentSerializer = new SystemTextJsonContentSerializer(
                            jsonSerializerOptions: new JsonSerializerOptions
                            {
                                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                                PropertyNameCaseInsensitive = true,
                                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                            }
                        ),
                    }
                )
                .ConfigureHttpClient(c =>
                {
                    c.BaseAddress = new Uri(ApiClient.ApiBaseUrl);
                    c.Timeout = TimeSpan.FromMinutes(2);
                })
                .ConfigurePrimaryHttpMessageHandler(() =>
                    new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = false }
                )
                .UseProxyForCategory(ProxyCategory.HosterUploads);

            services.AddScoped<IFilerApiClient, ApiClient>();
            services.AddKeyedScoped<IHoster, Filer>(nameof(Filer));
        }
    }
}
