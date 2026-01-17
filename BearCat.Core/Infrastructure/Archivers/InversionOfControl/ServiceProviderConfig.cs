using BearCat.Core.Domain.Abstractions.Archiver;
using BearCat.Core.Infrastructure.Archivers._7Zip;
using BearCat.Core.Infrastructure.Archivers.Rar;
using Microsoft.Extensions.DependencyInjection;

namespace BearCat.Core.Infrastructure.Archivers.InversionOfControl;

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
