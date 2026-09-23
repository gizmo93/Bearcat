using Bearcat.Abstractions.Hoster;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Hosters.Turbobit.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddTurbobit()
        {
            services.AddKeyedScoped<IHoster, Turbobit>(nameof(Turbobit));
        }
    }
}
