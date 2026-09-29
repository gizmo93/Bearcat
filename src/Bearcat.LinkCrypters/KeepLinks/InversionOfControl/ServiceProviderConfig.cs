using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.Proxies;
using Bearcat.LinkCrypters.KeepLinks.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.LinkCrypters.KeepLinks.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddKeepLinks()
        {
            services
                .AddRefitClient<IKeepLinksApi>(
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
                    c.BaseAddress = new Uri("https://www.keeplinks.org");
                })
                .UseProxyForCategory(ProxyCategory.LinkCrypters);

            services.AddKeyedScoped<ILinkCrypter, KeepLinks>(nameof(KeepLinks));
        }
    }
}
