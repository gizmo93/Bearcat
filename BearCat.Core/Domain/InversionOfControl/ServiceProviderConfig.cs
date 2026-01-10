using BearCat.Core.Domain.UseCases;
using BearCat.Core.Domain.UseCases.ManageDistributions;
using BearCat.Core.Domain.UseCases.ManageHosters;
using BearCat.Core.Domain.UseCases.ManageReleases;
using Microsoft.Extensions.DependencyInjection;

namespace BearCat.Core.Domain.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddDomain()
        {
            services.AddScoped<HosterInstanceService>();
            services.AddScoped<HosterRegistrationService>();
            services.AddScoped<DistributionPackingService>();
            services.AddScoped<DistributionUploadService>();
            services.AddScoped<ReleaseService>();
        }
    }
}
