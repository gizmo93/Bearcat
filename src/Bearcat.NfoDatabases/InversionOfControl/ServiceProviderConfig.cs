using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.NfoDatabases.Xrel;
using Bearcat.NfoDatabases.Xrel.Api;
using Bearcat.NfoDatabases.Xrel.InversionOfControl;
using Microsoft.Extensions.DependencyInjection;
using Refit;

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
