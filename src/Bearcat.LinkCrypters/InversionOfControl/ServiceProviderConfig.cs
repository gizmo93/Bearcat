using Bearcat.Abstractions.LinkCrypter;
using Bearcat.LinkCrypters.HideCx.InversionOfControl;
using Bearcat.LinkCrypters.KeepLinks.InversionOfControl;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.LinkCrypters.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddLinkCrypters()
        {
            services.AddHideCx();
            services.AddKeepLinks();
            services.AddScoped<ILinkCrypterFactory, LinkCrypterFactory>();
        }
    }
}
