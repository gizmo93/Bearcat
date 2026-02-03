using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Domain.Abstractions.Hoster;
using Bearcat.Hosters.Rapidgator.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.Hosters.Rapidgator.InversionOfControl;

public static class ServiceProviderConfig
{
    public static void AddRapidgator(this IServiceCollection services)
    {
        services.AddRefitClient<IRapidgatorApi>(
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
                c.BaseAddress = new Uri("https://rapidgator.net/");
                c.Timeout = Timeout.InfiniteTimeSpan;
            });

        services.AddScoped<ApiClient>();
        services.AddKeyedScoped<IHoster, Rapidgator>(nameof(Rapidgator));
    }
}
