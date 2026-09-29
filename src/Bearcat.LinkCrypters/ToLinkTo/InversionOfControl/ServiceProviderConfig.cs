using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.Proxies;
using Bearcat.LinkCrypters.ToLinkTo.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.LinkCrypters.ToLinkTo.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddToLinkTo()
        {
            services
                .AddRefitClient<IToLinkToApi>(
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
                    c.BaseAddress = new Uri("https://tolink.to");
                })
                .UseProxyForCategory(ProxyCategory.LinkCrypters);

            services.AddKeyedScoped<ILinkCrypter, ToLinkTo>(nameof(ToLinkTo));
        }
    }
}
