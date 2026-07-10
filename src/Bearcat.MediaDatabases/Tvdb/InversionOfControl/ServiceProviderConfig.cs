using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.MediaDatabases.Tvdb.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.MediaDatabases.Tvdb.InversionOfControl;

public static class ServiceProviderConfig
{
    public static void AddTvdb(this IServiceCollection services)
    {
        services
            .AddRefitClient<ITvdbApi>(
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
                client.BaseAddress = new Uri("https://api4.thetvdb.com/");
            });

        services.AddSingleton<TvdbTokenProvider>();
        services.AddScoped<TvdbClient>();
        services.AddKeyedScoped<IMediaMetadataDatabase, TvdbMetadataDatabase>(
            nameof(TvdbMetadataDatabase)
        );
    }
}
