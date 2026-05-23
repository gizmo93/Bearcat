using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster;
using Bearcat.Hosters.Alfafile.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.Hosters.Alfafile.InversionOfControl;

public static class ServiceProviderConfig
{
    public static void AddAlfafile(this IServiceCollection services)
    {
        services
            .AddRefitClient<IAlfafileApi>(
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
                c.BaseAddress = new Uri("https://alfafile.net/");
                c.Timeout = Timeout.InfiniteTimeSpan;
            });

        services.AddScoped<IAlfafileApiClient, ApiClient>();
        services.AddKeyedScoped<IHoster, Alfafile>(nameof(Alfafile));
    }
}
