using BearCat.Core.Domain.UseCases.ManageDistributions.Repositories;
using BearCat.Core.Domain.UseCases.ManageHosters.Repositories;
using BearCat.Core.Domain.UseCases.ManageReleases.Repositories;
using BearCat.Core.Infrastructure.Database.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace BearCat.Core.Infrastructure.Database.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddRepositories()
        {
            services.AddScoped<IHosterConfigurationReadRepository, HosterConfigurationRepository>();
            services.AddScoped<IHosterConfigurationWriteRepository, HosterConfigurationRepository>();
            services.AddScoped<IDistributionWriteRepository, DistributionRepository>();
            services.AddScoped<IDistributionReadRepository, DistributionRepository>();
            services.AddScoped<IReleaseWriteRepository, ReleaseRepository>();
        }
    }
}
