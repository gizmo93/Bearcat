using BearCat.Core.Domain.Abstractions.Hoster;
using BearCat.Core.Infrastructure.Hosters.DDownload.InversionOfControl;
using BearCat.Core.Infrastructure.Hosters.Rapidgator.InversionOfControl;
using Microsoft.Extensions.DependencyInjection;

namespace BearCat.Core.Infrastructure.Hosters.InversionOfControl;

public static class ServiceProviderConfig
{
    public static void AddHosters(this IServiceCollection services)
    {
        services.AddRapidgator();
        services.AddDdownload();
        services.AddScoped<IHosterFactory, HosterFactory>();
    }
}
