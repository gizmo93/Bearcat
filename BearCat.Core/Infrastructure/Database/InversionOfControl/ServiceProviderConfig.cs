using BearCat.Core.Domain.UseCases.ManageHosters.Repositories;
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
        }
    }
}
