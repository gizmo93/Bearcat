using BearCat.Core.Domain.UseCases;
using BearCat.Core.Domain.UseCases.ManageHosters;
using BearCat.Core.Infrastructure.Hosters;
using Microsoft.Extensions.DependencyInjection;

namespace BearCat.Core.Domain.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddDomain()
        {
            services.AddScoped<HosterService>();
            services.AddScoped<HosterInstanceService>();
            services.AddScoped<HosterRegistrationService>();
        }
    }
}
