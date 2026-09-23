using Bearcat.Abstractions.Hoster;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Hosters.Hitfile.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddHitfile()
        {
            services.AddKeyedScoped<IHoster, Hitfile>(nameof(Hitfile));
        }
    }
}
