using Bearcat.LinkCrypters.HideCx.InversionOfControl;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.LinkCrypters.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddLinkCrypters()
        {
            services.AddHideCx();
        }
    }
}
