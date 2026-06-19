using Bearcat.Website.Pages.PostQueue;
using BlazorBlueprint.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website;

public static class ServiceProviderConfig
{
    public static IServiceCollection AddBearcatBlueprintComponents(this IServiceCollection services)
    {
        services.AddBlazorBlueprintComponents();
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        services.AddHttpClient("cover-download");
        services.AddScoped<PostQueueWorkflowState>();
        services
            .AddControllers()
            .AddApplicationPart(typeof(ServiceProviderConfig).Assembly)
            .AddControllersAsServices();
        return services;
    }
}
