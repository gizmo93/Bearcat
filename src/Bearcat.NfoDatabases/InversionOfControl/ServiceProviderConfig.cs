using Bearcat.NfoDatabases.Xrel.InversionOfControl;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.NfoDatabases.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddNfoDatabases()
        {
            services.AddXrel();
        }
    }
}
