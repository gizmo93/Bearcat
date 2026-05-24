using Bearcat.Abstractions;
using Bearcat.Abstractions.Configurations;
using Bearcat.Infrastructure.Configuration;
using Bearcat.Infrastructure.Database.InversionOfControl;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Infrastructure.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddInfrastructure(IConfiguration configuration)
        {
            services.AddSingleton<IEncryptionKeyProvider, FileEncryptionKeyProvider>();
            services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();
            services.AddScoped<RegistrationSecretMigration>();
            services.AddDatabase(configuration);
            services.AddScoped<IFileSystemService, FileSystemService>();
            services.AddSingleton<
                IApplicationConfigurationOverrideCache,
                ApplicationConfigurationOverrideCache
            >();
        }
    }
}
