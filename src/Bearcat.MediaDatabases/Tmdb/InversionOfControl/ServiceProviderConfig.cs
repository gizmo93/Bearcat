using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Abstractions.Proxies;
using Bearcat.MediaDatabases.Tmdb.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.MediaDatabases.Tmdb.InversionOfControl;

public static class ServiceProviderConfig
{
    public static void AddTmdb(this IServiceCollection services)
    {
        services
            .AddRefitClient<ITmdbApi>()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://api.themoviedb.org/");
            })
            .UseProxyForCategory(ProxyCategory.MediaDatabases);

        services.AddKeyedScoped<IMediaMetadataDatabase, TmdbMetadataDatabase>(
            nameof(TmdbMetadataDatabase)
        );
    }
}
