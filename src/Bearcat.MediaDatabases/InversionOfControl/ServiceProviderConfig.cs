using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.MediaDatabases.Tmdb.InversionOfControl;
using Bearcat.MediaDatabases.Tvdb.InversionOfControl;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.MediaDatabases.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddMediaDatabases()
        {
            services.AddTvdb();
            services.AddTmdb();
            services.AddScoped<IMediaMetadataDatabaseFactory, MediaMetadataDatabaseFactory>();
        }
    }
}
