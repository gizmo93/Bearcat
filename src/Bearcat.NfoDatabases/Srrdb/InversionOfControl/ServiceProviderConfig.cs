using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.NfoDatabases.Srrdb.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.NfoDatabases.Srrdb.InversionOfControl;

public static class ServiceProviderConfig
{
    public static void AddSrrdb(this IServiceCollection services)
    {
        services
            .AddRefitClient<ISrrdbApi>(
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
                client.BaseAddress = new Uri("https://api.srrdb.com/");
            });

        services.AddHttpClient(
            "SrrdbNfoDownload",
            client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Bearcat/1.0");
            }
        );
        services.AddScoped<SrrdbClient>();
        services.AddKeyedScoped<INfoDatabase, SrrdbNfoDatabase>(nameof(SrrdbNfoDatabase));
    }
}
