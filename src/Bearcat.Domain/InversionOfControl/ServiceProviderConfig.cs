using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.Shared.QualityGate;
using Bearcat.Domain.Shared.QualityGate.Checks;
using Bearcat.Domain.UseCases.ManageApplicationConfigurations;
using Bearcat.Domain.UseCases.ManageArchiveConfigs;
using Bearcat.Domain.UseCases.ManageArchives;
using Bearcat.Domain.UseCases.ManageBackgroundTasks;
using Bearcat.Domain.UseCases.ManageDistributionSites;
using Bearcat.Domain.UseCases.ManageForumPostTemplates;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Rendering;
using Bearcat.Domain.UseCases.ManageHosters;
using Bearcat.Domain.UseCases.ManageImageHosters;
using Bearcat.Domain.UseCases.ManageImageUploadConfigs;
using Bearcat.Domain.UseCases.ManageImageUploads;
using Bearcat.Domain.UseCases.ManageLinkCrypterContainers;
using Bearcat.Domain.UseCases.ManageLinkCrypters;
using Bearcat.Domain.UseCases.ManageNfoDatabases;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.UseCases.ManagePostedLocations;
using Bearcat.Domain.UseCases.ManageQualityProfiles;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations;
using Bearcat.Domain.UseCases.ManageReleaseGroups;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageReleaseTemplates;
using Bearcat.Domain.UseCases.ManageSeriesDatabases;
using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters;
using Bearcat.Domain.UseCases.ManageUploadConfigs;
using Bearcat.Domain.UseCases.ManageUploads;
using Bearcat.Domain.UseCases.ManageUploads.Progress;
using Bearcat.Domain.UseCases.ResolveMediaMetadata;
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
            services.AddScoped<ImageHosterService>();
            services.AddScoped<DistributionSiteSessionService>();
            services.AddScoped<DistributionSiteRegistrationService>();
            services.AddScoped<ImageUploadConfigService>();
            services.AddScoped<PostedLocationService>();
            services.AddScoped<ImageUploadService>();
            services.AddScoped<BackgroundTaskStateService>();
            services.AddScoped<ReleaseFolderAutomationService>();
            services.AddScoped<AutomaticallyCreateReleasesService>();
            services.AddScoped<ReleaseCollectionService>();
            services.AddScoped<ReleaseCollectionAssignmentService>();
            services.AddScoped<ReleaseCollectionInfoResolutionService>();
            services.AddScoped<ReleaseGroupService>();
            services.AddScoped<QualityProfileService>();
            services.AddScoped<ReleaseService>();
            services.AddScoped<ForumPostRenderService>();
            services.AddScoped<ReleaseForumPostUploadBuilder>();
            services.AddScoped<ForumPostImageLinkBuilder>();
            services.AddScoped<IForumPostRenderSource, ReleaseForumPostRenderSource>();
            services.AddScoped<IForumPostRenderSource, ReleaseCollectionForumPostRenderSource>();
            services.AddScoped<ReleaseInfoService>();
            services.AddScoped<ReleaseInfoResolutionService>();
            services.AddScoped<MediaMetadataResolver>();
            services.AddScoped<MediaMetadataService>();
            services.AddScoped<ReleaseTemplateService>();
            services.AddScoped<ForumPostTemplateService>();
            services.AddScoped<ArchiveCreationService>();
            services.AddScoped<ArchiveCleanupService>();
            services.AddScoped<UploadFilesService>();
            services.AddScoped<UploadFinalizationService>();
            services.AddScoped<FileUploadExecutionService>();
            services.AddScoped<MissingFileValidationService>();
            services.AddScoped<UploadConcurrencyService>();
            services.AddSingleton<IUploadProgressTracker, UploadProgressTracker>();
            services.AddScoped<UploadStateService>();
            services.AddScoped<UploadStateService>();
            services.AddScoped<QualityGateEvaluator>();
            services.AddScoped<QualityGateService>();
            services.AddScoped<IQualityCheck, FilePatternQualityCheck>();
            services.AddScoped<IQualityCheck, MinimumFolderSizeQualityCheck>();
            services.AddScoped<IQualityCheck, RequiredReleaseInfoQualityCheck>();
            services.AddScoped<IQualityCheck, MediaInfoQualityCheck>();
            services.AddScoped<QualityCheckCatalog>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<HosterCaptchaVerificationService>();
            services.AddScoped<ArchiveConfigService>();
            services.AddScoped<TimeProvider>();
            services.AddScoped<UploadConfigService>();
            services.AddScoped<LinkCrypterService>();
            services.AddScoped<NfoDatabaseRegistrationService>();
            services.AddScoped<SeriesDatabaseRegistrationService>();
            services.AddScoped<CollectionLinkCrypterContainerService>();
            services.AddScoped<LinkCrypterContainerService>();
            services.AddScoped<UploadConfigLinkCrypterService>();
            services.AddApplicationConfiguration<ArchiveCleanupConfiguration>();
            services.AddApplicationConfiguration<ArchiveRepackagingConfiguration>();
            services.AddApplicationConfiguration<InitialUploadConfiguration>();
            services.AddApplicationConfiguration<FolderAutomationConfiguration>();
            services.AddApplicationConfiguration<UploadConcurrencyConfiguration>();
            services.AddApplicationConfiguration<PostQueueConfiguration>();
            services.AddSingleton<ApplicationConfigurationRegistry>();
            services.AddScoped<
                IApplicationConfigurationProvider,
                ApplicationConfigurationProvider
            >();
            services.AddScoped<ApplicationConfigurationService>();
        }
    }
}
