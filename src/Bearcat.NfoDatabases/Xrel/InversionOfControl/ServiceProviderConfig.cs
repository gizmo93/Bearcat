using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.NfoDatabases.Xrel.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.NfoDatabases.Xrel.InversionOfControl;

public static class ServiceProviderConfig
{
    public static void AddXrel(this IServiceCollection services)
    {
        services
            .AddRefitClient<IXrelApi>(
                new RefitSettings
                {
                    ContentSerializer = new SystemTextJsonContentSerializer(
                        new JsonSerializerOptions
                        {
                            NumberHandling = JsonNumberHandling.AllowReadingFromString,
                            PropertyNameCaseInsensitive = true,
                        }
                    ),
                }
            )
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://xrel-api.nfos.to/");
            });

        services.AddSingleton<XrelRateLimitState>();
        services.AddScoped<XrelClient>();
        services.AddKeyedScoped<INfoDatabase, XrelNfoDatabase>(nameof(XrelNfoDatabase));
        services.AddScoped<INfoDatabaseFactory, NfoDatabaseFactory>();
    }
}
