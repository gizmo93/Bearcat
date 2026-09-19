using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.MediaDatabases.Steam.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.MediaDatabases.Steam.InversionOfControl;

public static class ServiceProviderConfig
{
    public static void AddSteam(this IServiceCollection services)
    {
        services
            .AddRefitClient<ISteamApi>()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://store.steampowered.com");
            });

        services.AddKeyedScoped<IMediaMetadataDatabase, SteamMetadataDatabase>(
            nameof(SteamMetadataDatabase)
        );
    }
}
