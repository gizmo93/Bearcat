using BearCat.Core.Domain.UseCases.ManageArchiveConfigs;
using BearCat.Core.Domain.UseCases.ManageArchives.Repositories;
using BearCat.Core.Domain.UseCases.ManageHosters.Repositories;
using BearCat.Core.Domain.UseCases.ManageNotifications.Repositories;
using BearCat.Core.Domain.UseCases.ManageReleases.Repositories;
using BearCat.Core.Domain.UseCases.ManageUploadConfigs.Repositories;
using BearCat.Core.Domain.UseCases.ManageUploads.Repositories;
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
        }
    }
}
