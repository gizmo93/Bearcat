using Bearcat.Domain.Abstractions;
using Bearcat.Infrastructure.Database.InversionOfControl;
using Bearcat.Infrastructure.FileSystem;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Infrastructure.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddInfrastructure(IConfiguration configuration)
        {
            services.AddDatabase(configuration);
            services.AddScoped<IFileSystemService, FileSystemService>();
        }
    }
}
