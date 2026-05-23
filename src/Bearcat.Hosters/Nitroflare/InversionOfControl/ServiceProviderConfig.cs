using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster;
using Bearcat.Hosters.Nitroflare.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.Hosters.Nitroflare.InversionOfControl;

public static class ServiceProviderConfig
{
    public static void AddNitroflare(this IServiceCollection services)
    {
        services
            .AddRefitClient<INitroflareApi>(
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
                c.BaseAddress = new Uri("https://nitroflare.com/");
                c.Timeout = Timeout.InfiniteTimeSpan;
            });

        services.AddScoped<INitroflareApiClient, ApiClient>();
        services.AddKeyedScoped<IHoster, Nitroflare>(nameof(Nitroflare));
    }
}
