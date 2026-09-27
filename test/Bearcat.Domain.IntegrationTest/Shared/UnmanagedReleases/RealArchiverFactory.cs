using Bearcat.Archivers;
using Bearcat.Archivers.InversionOfControl;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bearcat.Domain.IntegrationTest.Shared.UnmanagedReleases;

public sealed class RealArchiverFactory() : ArchiverFactory(CreateServiceProvider())
{
    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddArchivers();

        return services.BuildServiceProvider();
    }
}
