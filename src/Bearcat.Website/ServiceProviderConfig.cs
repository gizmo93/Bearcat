using Bearcat.Website.Layout;
using Bearcat.Website.Pages.PostQueue;
using BlazorBlueprint.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website;

public static class ServiceProviderConfig
{
    public static IServiceCollection AddBearcatBlueprintComponents(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddBlazorBlueprintComponents();
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        services.AddHttpClient("cover-download");
        services.AddScoped<PostQueueWorkflowState>();
        services.AddScoped<NavMenuState>();
        services
            .AddControllers()
            .AddApplicationPart(typeof(ServiceProviderConfig).Assembly)
            .AddControllersAsServices();
        services.Configure<WorkingDirectoriesConfig>(configuration);
        return services;
    }
}
