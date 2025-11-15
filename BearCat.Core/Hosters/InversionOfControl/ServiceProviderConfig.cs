using BearCat.Core.Hosters.Rapidgator.InversionOfControl;
using Microsoft.Extensions.DependencyInjection;

namespace BearCat.Core.Hosters.InversionOfControl;

public static class ServiceProviderConfig
{
    public static void AddHosters(this IServiceCollection services)
    {
        services.AddRapidgator();
        services.AddScoped<HosterService>();
    }
}