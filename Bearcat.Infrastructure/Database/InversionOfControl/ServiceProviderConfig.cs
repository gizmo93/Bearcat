using Bearcat.Domain.UseCases.CreateLinkCrypterContainers.Repositories;
using Bearcat.Domain.UseCases.ManageArchiveConfigs;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.UseCases.ManageHosters.Repositories;
using Bearcat.Domain.UseCases.ManageLinkCrypters.Repositories;
using Bearcat.Domain.UseCases.ManageNotifications.Repositories;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.Repositories;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Repositories;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Bearcat.Infrastructure.Database.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Infrastructure.Database.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddDatabase(IConfiguration configuration)
        {
            services.AddDbContext<BearcatDbContext>(builder =>
            {
                var connectionString = configuration.GetRequiredSection("Database:ConnectionString").Value;
                builder.UseNpgsql(connectionString);
            }, ServiceLifetime.Transient);

            services.AddScoped<IBearcatWriteDbContext>(s => s.GetRequiredService<BearcatDbContext>());
            services.AddScoped<IBearcatReadDbContext>(s =>
            {
                var dbContext = s.GetRequiredService<BearcatDbContext>();
                dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                return dbContext;
            });

            services.AddRepositories();
        }

        private void AddRepositories()
        {
            services.AddScoped<IHosterConfigurationReadRepository, HosterConfigurationRepository>();
            services.AddScoped<IHosterConfigurationWriteRepository, HosterConfigurationRepository>();
            services.AddScoped<IReleaseWriteRepository, ReleaseWriteRepository>();
            services.AddScoped<IArchiveCreationRepository, ArchiveCreationRepository>();
            services.AddScoped<IUploadFilesRepository, UploadFilesRepository>();
            services.AddScoped<IUploadStateRepository, UploadStateRepository>();
            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddScoped<IReleaseReadRepository, ReleaseReadRepository>();
            services.AddScoped<IArchiveConfigWriteRepository, ArchiveConfigWriteRepository>();
            services.AddScoped<IArchiveReadRepository, ArchiveReadRepository>();
            services.AddScoped<IUploadConfigReadRepository, UploadConfigReadRepository>();
            services.AddScoped<IUploadConfigWriteRepository, UploadConfigWriteRepository>();
            services.AddScoped<ILinkCrypterRegistrationWriteRepository, LinkCrypterRegistrationWriteRepository>();
            services.AddScoped<ILinkCrypterRegistrationReadRepository, LinkCrypterRegistrationReadRepository>();
            services
                .AddScoped<ILinkCrypterContainerCreationWriteRepository, LinkCrypterContainerCreationWriteRepository>();
            services.AddScoped<IUploadConfigLinkCrypterReadRepository, UploadConfigLinkCrypterReadRepository>();
            services.AddScoped<IUploadConfigLinkCrypterWriteRepository, UploadConfigLinkCrypterWriteRepository>();
        }
    }
}
