using Bearcat.Abstractions.RemoteSource;
using Bearcat.RemoteSources.Ftp;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.RemoteSources.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddRemoteSources()
        {
            services.AddKeyedScoped<IRemoteSource, FtpRemoteSource>(nameof(FtpRemoteSource));
            services.AddScoped<IRemoteSourceFactory, RemoteSourceFactory>();
        }
    }
}
