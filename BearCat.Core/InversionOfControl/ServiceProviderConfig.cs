using BearCat.Core.Application.BackgroundTasks;
using BearCat.Core.Domain.InversionOfControl;
using BearCat.Core.Infrastructure.Archivers.InversionOfControl;
using BearCat.Core.Infrastructure.Database;
using BearCat.Core.Infrastructure.Database.InversionOfControl;
using BearCat.Core.Infrastructure.Hosters.InversionOfControl;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BearCat.Core.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddCore(IConfiguration configuration)
        {
            services.AddHttpClient();
            services.AddHosters();
            services.AddDatabase(configuration);
            services.AddRepositories();
            services.AddDomain();
            services.AddArchivers();
            //services.AddHostedServices();
        }

        private void AddHostedServices()
        {
            services.AddHostedService<ArchivingBackgroundTask>();
            services.AddHostedService<ArchiveUploadBackgroundTask>();
            services.AddHostedService<CheckUploadStateBackgroundTask>();
        }

        private void AddDatabase(IConfiguration configuration)
        {
            services.AddDbContext<BearcatDbContext>(builder =>
            {
                var connectionString = configuration.GetValue<string>("Database:ConnectionString");
                builder.UseNpgsql(connectionString);
            }, ServiceLifetime.Transient);

            services.AddScoped<IBearcatWriteDbContext>(s => s.GetRequiredService<BearcatDbContext>());
            services.AddScoped<IBearcatReadDbContext>(s =>
            {
                var dbContext = s.GetRequiredService<BearcatDbContext>();
                dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                return dbContext;
            });
        }
    }
}
