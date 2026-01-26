using Bearcat.Archivers._7Zip;
using Bearcat.Archivers.Rar;
using Bearcat.Domain.Abstractions.Archiver;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Archivers.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddArchivers()
        {
            services.AddKeyedScoped<IArchiver, RarArchiver>(nameof(RarArchiver));
            services.AddKeyedScoped<IArchiver, SevenZipArchiver>(nameof(SevenZipArchiver));
            services.AddScoped<IArchiverFactory, ArchiverFactory>();
        }
    }
}
