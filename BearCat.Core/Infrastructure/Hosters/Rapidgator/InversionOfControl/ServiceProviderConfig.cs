using System.Text.Json;
using System.Text.Json.Serialization;
using BearCat.Core.Domain.Abstractions.Hoster;
using BearCat.Core.Infrastructure.Hosters.Rapidgator.ApiClient;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace BearCat.Core.Infrastructure.Hosters.Rapidgator.InversionOfControl;

public static class ServiceProviderConfig
{
    public static void AddRapidgator(this IServiceCollection services)
    {
        services.AddRefitClient<IRapidgatorApi>(settings: new RefitSettings
        {
            ContentSerializer = new SystemTextJsonContentSerializer(
                    jsonSerializerOptions: new JsonSerializerOptions
                    {
                        NumberHandling = JsonNumberHandling.AllowReadingFromString,
                        PropertyNameCaseInsensitive = true,
                    })
        })
            .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://rapidgator.net/"));
        services.AddScoped<RapidgatorApiClient>();
        services.AddKeyedScoped<IHoster, Rapidgator>(nameof(Rapidgator));
    }
}
