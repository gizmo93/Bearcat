using Bearcat.Abstractions.LinkCrypter;
using Bearcat.LinkCrypters.FileCrypt.InversionOfControl;
using Bearcat.LinkCrypters.HideCx.InversionOfControl;
using Bearcat.LinkCrypters.KeepLinks.InversionOfControl;
using Bearcat.LinkCrypters.ToLinkTo.InversionOfControl;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.LinkCrypters.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddLinkCrypters()
        {
            services.AddFileCrypt();
            services.AddHideCx();
            services.AddKeepLinks();
            services.AddToLinkTo();
            services.AddScoped<ILinkCrypterFactory, LinkCrypterFactory>();
        }
    }
}
