using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Bearcat.Website;

public static class ServiceProviderConfig
{
    public static IServiceCollection AddBearcatComponents(this IServiceCollection services)
    {
        // Add MudBlazor services
        services.AddMudServices(cfg =>
        {
            cfg.SnackbarConfiguration.ShowTransitionDuration = 50;
            cfg.SnackbarConfiguration.HideTransitionDuration = 50;
        });

        return services;
    }
}
