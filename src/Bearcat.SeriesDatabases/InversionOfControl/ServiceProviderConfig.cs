using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.SeriesDatabases.Tmdb.InversionOfControl;
using Bearcat.SeriesDatabases.Tvdb.InversionOfControl;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.SeriesDatabases.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddSeriesDatabases()
        {
            services.AddTvdb();
            services.AddTmdb();
            services.AddScoped<IMediaMetadataDatabaseFactory, MediaMetadataDatabaseFactory>();
        }
    }
}
