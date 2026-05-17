using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Abstractions.Configurations;

public static class ApplicationConfigurationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationConfiguration<TConfiguration>(
        this IServiceCollection services
    )
        where TConfiguration : class, IApplicationConfiguration, new()
    {
        services.AddTransient<TConfiguration>();
        services.AddSingleton(new ApplicationConfigurationRegistration(typeof(TConfiguration)));

        return services;
    }
}
