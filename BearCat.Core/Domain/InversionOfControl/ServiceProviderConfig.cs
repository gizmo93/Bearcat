using BearCat.Core.Domain.Shared;
using BearCat.Core.Domain.UseCases;
using BearCat.Core.Domain.UseCases.ManageArchives;
using BearCat.Core.Domain.UseCases.ManageHosters;
using BearCat.Core.Domain.UseCases.ManageNotifications;
using BearCat.Core.Domain.UseCases.ManageReleases;
using BearCat.Core.Domain.UseCases.ManageUploads;
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
            services.AddScoped<ReleaseService>();
            services.AddScoped<ArchiveCreationService>();
            services.AddScoped<UploadFilesService>();
            services.AddScoped<UploadStateService>();
            services.AddScoped<UploadStateService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<ArchiverInstanceService>();
        }
    }
}
