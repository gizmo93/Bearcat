using Bearcat.Application.BackgroundTasks;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Application.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddApplication()
        {
            services.AddHostedService<ArchivingBackgroundTask>();
            services.AddHostedService<ArchiveUploadBackgroundTask>();
            services.AddHostedService<CheckUploadStateBackgroundTask>();
            services.AddHostedService<LinkCrypterContainerBackgroundTask>();
        }
    }
}
