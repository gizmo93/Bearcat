using BlazorBlueprint.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Blueprint;

public static class ServiceProviderConfig
{
    public static IServiceCollection AddBearcatBlueprintComponents(this IServiceCollection services)
    {
        services.AddBlazorBlueprintComponents();
        return services;
    }
}
