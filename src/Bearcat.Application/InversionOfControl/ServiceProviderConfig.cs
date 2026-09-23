using Bearcat.Application.BackgroundTasks;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Application.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddApplication()
        {
            services.AddHostedService<ConfigurationCacheRefreshBackgroundTask>();
            services.AddHostedService<ReleaseFolderAutomationBackgroundTask>();
            services.AddHostedService<RemoteSourceScanBackgroundTask>();
            services.AddHostedService<RemoteSourceDownloadBackgroundTask>();
            services.AddHostedService<RemoteDownloadReleaseCreationBackgroundTask>();
            services.AddHostedService<RemoteDownloadRawFileCleanupBackgroundTask>();
            services.AddHostedService<ReleaseInfoResolutionBackgroundTask>();
            services.AddHostedService<ReleaseCollectionInfoResolutionBackgroundTask>();
            services.AddHostedService<ArchivingBackgroundTask>();
            services.AddHostedService<ArchiveCleanupBackgroundTask>();
            services.AddHostedService<ArchiveUploadBackgroundTask>();
            services.AddHostedService<ImageUploadBackgroundTask>();
            services.AddHostedService<CheckUploadStateBackgroundTask>();
            services.AddHostedService<LinkCrypterContainerBackgroundTask>();
            services.AddHostedService<QualityGateReevaluationBackgroundTask>();
            services.AddHostedService<ReleaseClassificationBackgroundTask>();
            services.AddHostedService<TelegramNotificationBackgroundTask>();
            services.AddHostedService<AutoForumPostingBackgroundTask>();
        }
    }
}
