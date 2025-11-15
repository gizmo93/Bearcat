using BearCat.Core.Hosters.InversionOfControl;
using Microsoft.Extensions.DependencyInjection;

namespace BearCat.Core.InversionOfControl;

public static class ServiceProviderConfig
{
    public static void AddCore(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddHosters();
    }
}