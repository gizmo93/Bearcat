using Bearcat.Domain.Abstractions.Hoster;
using Bearcat.Hosters.DDownload.InversionOfControl;
using Bearcat.Hosters.Rapidgator.InversionOfControl;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Hosters.InversionOfControl;

public static class ServiceProviderConfig
{
    public static void AddHosters(this IServiceCollection services)
    {
        services.AddRapidgator();
        services.AddDdownload();
        services.AddScoped<IHosterFactory, HosterFactory>();
    }
}
