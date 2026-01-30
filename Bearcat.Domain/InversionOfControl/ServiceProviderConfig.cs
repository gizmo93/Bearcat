using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageArchiveConfigs;
using Bearcat.Domain.UseCases.ManageArchives;
using Bearcat.Domain.UseCases.ManageHosters;
using Bearcat.Domain.UseCases.ManageLinkCrypterContainers;
using Bearcat.Domain.UseCases.ManageLinkCrypters;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters;
using Bearcat.Domain.UseCases.ManageUploadConfigs;
using Bearcat.Domain.UseCases.ManageUploads;
using Microsoft.Extensions.DependencyInjection;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddDomain()
        {
            services.AddScoped<HosterRegistrationService>();
            services.AddScoped<ReleaseService>();
            services.AddScoped<ArchiveCreationService>();
            services.AddScoped<UploadFilesService>();
            services.AddScoped<UploadStateService>();
            services.AddScoped<UploadStateService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<ArchiveConfigService>();
            services.AddScoped<TimeProvider>();
            services.AddScoped<UploadConfigService>();
            services.AddScoped<LinkCrypterService>();
            services.AddScoped<LinkCrypterContainerService>();
            services.AddScoped<UploadConfigLinkCrypterService>();
        }
    }
}
