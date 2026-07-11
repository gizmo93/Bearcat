using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.UseCases.Dashboard.Repositories;
using Bearcat.Domain.UseCases.ManageApplicationConfigurations;
using Bearcat.Domain.UseCases.ManageArchiveConfigs;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.UseCases.ManageBackgroundTasks.Repositories;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Repositories;
using Bearcat.Domain.UseCases.ManageHosters.Repositories;
using Bearcat.Domain.UseCases.ManageImageHosters.Repositories;
using Bearcat.Domain.UseCases.ManageImageUploadConfigs.Repositories;
using Bearcat.Domain.UseCases.ManageImageUploads.Repositories;
using Bearcat.Domain.UseCases.ManageLinkCrypterContainers.Repositories;
using Bearcat.Domain.UseCases.ManageLinkCrypters.Repositories;
using Bearcat.Domain.UseCases.ManageMediaDatabases.Repositories;
using Bearcat.Domain.UseCases.ManageNfoDatabases.Repositories;
using Bearcat.Domain.UseCases.ManageNotifications.Repositories;
using Bearcat.Domain.UseCases.ManagePostedLocations.Repositories;
using Bearcat.Domain.UseCases.ManageQualityProfiles.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseGroups.Repositories;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.Repositories;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Repositories;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Bearcat.Domain.UseCases.ResolveMediaMetadata;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.DistributionSites;
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
            services.AddDbContextFactory<BearcatDbContext>(builder =>
            {
                var connectionString = configuration
                    .GetRequiredSection("Database:ConnectionString")
                    .Value;
                builder.UseNpgsql(
                    connectionString,
                    opts => opts.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
                );
            });

            services.AddScoped<IBearcatWriteDbContext>(s =>
                s.GetRequiredService<BearcatDbContext>()
            );
            services.AddScoped<IBearcatReadDbContext>(s =>
                s.GetRequiredService<BearcatDbContext>()
            );

            services.AddRepositories();

            services.AddScoped<IDistributionSessionStore, DatabaseDistributionSessionStore>();
        }

        private void AddRepositories()
        {
            services.AddScoped<IDashboardReadRepository, DashboardReadRepository>();
            services.AddScoped<IHosterConfigurationReadRepository, HosterConfigurationRepository>();
            services.AddScoped<
                IImageHosterRegistrationReadRepository,
                ImageHosterRegistrationReadRepository
            >();
            services.AddScoped<IImageUploadConfigReadRepository, ImageUploadConfigReadRepository>();
            services.AddScoped<IPostedLocationReadRepository, PostedLocationReadRepository>();
            services.AddScoped<IPostedLocationWriteRepository, PostedLocationWriteRepository>();
            services.AddScoped<IBackgroundTaskStateReadRepository, BackgroundTaskStateRepository>();
            services.AddScoped<
                IBackgroundTaskStateWriteRepository,
                BackgroundTaskStateRepository
            >();
            services.AddScoped<
                IHosterConfigurationWriteRepository,
                HosterConfigurationRepository
            >();
            services.AddScoped<
                IImageHosterRegistrationWriteRepository,
                ImageHosterRegistrationWriteRepository
            >();
            services.AddScoped<
                IDistributionSiteRegistrationWriteRepository,
                DistributionSiteRegistrationWriteRepository
            >();
            services.AddScoped<
                IDistributionSiteRegistrationReadRepository,
                DistributionSiteRegistrationReadRepository
            >();
            services.AddScoped<
                IImageUploadConfigWriteRepository,
                ImageUploadConfigWriteRepository
            >();
            services.AddScoped<IImageUploadRepository, ImageUploadRepository>();
            services.AddScoped<IReleaseWriteRepository, ReleaseWriteRepository>();
            services.AddScoped<IReleaseInfoRepository, ReleaseInfoRepository>();
            services.AddScoped<IMediaMetadataRepository, MediaMetadataRepository>();
            services.AddScoped<IReleaseTemplateReadRepository, ReleaseTemplateRepository>();
            services.AddScoped<IReleaseTemplateWriteRepository, ReleaseTemplateRepository>();
            services.AddScoped<IForumPostTemplateReadRepository, ForumPostTemplateRepository>();
            services.AddScoped<IForumPostTemplateWriteRepository, ForumPostTemplateRepository>();
            services.AddScoped<IReleaseGroupReadRepository, ReleaseGroupRepository>();
            services.AddScoped<IReleaseGroupWriteRepository, ReleaseGroupRepository>();
            services.AddScoped<IQualityProfileReadRepository, QualityProfileRepository>();
            services.AddScoped<IQualityProfileWriteRepository, QualityProfileRepository>();
            services.AddScoped<IArchiveCreationRepository, ArchiveCreationRepository>();
            services.AddScoped<IArchiveCleanupRepository, ArchiveCleanupRepository>();
            services.AddScoped<IUploadFilesRepository, UploadFilesRepository>();
            services.AddScoped<IUploadStateRepository, UploadStateRepository>();
            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddScoped<INotificationReadRepository, NotificationReadRepository>();
            services.AddScoped<
                IReleaseFolderAutomationReadRepository,
                ReleaseFolderAutomationRepository
            >();
            services.AddScoped<
                IReleaseFolderAutomationWriteRepository,
                ReleaseFolderAutomationRepository
            >();
            services.AddScoped<
                IAutomaticallyCreateReleasesRepository,
                ReleaseFolderAutomationRepository
            >();
            services.AddScoped<IReleaseReadRepository, ReleaseReadRepository>();
            services.AddScoped<IQualityGateRepository, QualityGateRepository>();
            services.AddScoped<IReleaseForumPostUploadRepository>(serviceProvider =>
                (ReleaseReadRepository)serviceProvider.GetRequiredService<IReleaseReadRepository>()
            );
            services.AddScoped<IForumPostImageLinkRepository>(serviceProvider =>
                (ReleaseReadRepository)serviceProvider.GetRequiredService<IReleaseReadRepository>()
            );
            services.AddScoped<IReleaseCollectionReadRepository, ReleaseCollectionRepository>();
            services.AddScoped<IReleaseCollectionWriteRepository, ReleaseCollectionRepository>();
            services.AddScoped<
                IReleaseCollectionForumPostRepository,
                ReleaseCollectionForumPostRepository
            >();
            services.AddScoped<IArchiveConfigWriteRepository, ArchiveConfigWriteRepository>();
            services.AddScoped<IArchiveReadRepository, ArchiveReadRepository>();
            services.AddScoped<
                IApplicationConfigurationOverrideReadRepository,
                ApplicationConfigurationOverrideRepository
            >();
            services.AddScoped<
                IApplicationConfigurationOverrideWriteRepository,
                ApplicationConfigurationOverrideRepository
            >();
            services.AddScoped<IUploadConfigReadRepository, UploadConfigReadRepository>();
            services.AddScoped<IUploadConfigWriteRepository, UploadConfigWriteRepository>();
            services.AddScoped<
                ILinkCrypterRegistrationWriteRepository,
                LinkCrypterRegistrationWriteRepository
            >();
            services.AddScoped<
                ILinkCrypterRegistrationReadRepository,
                LinkCrypterRegistrationReadRepository
            >();
            services.AddScoped<
                ILinkCrypterContainerCreationWriteRepository,
                LinkCrypterContainerCreationWriteRepository
            >();
            services.AddScoped<
                IUploadConfigLinkCrypterReadRepository,
                UploadConfigLinkCrypterReadRepository
            >();
            services.AddScoped<
                IUploadConfigLinkCrypterWriteRepository,
                UploadConfigLinkCrypterWriteRepository
            >();
            services.AddScoped<
                INfoDatabaseRegistrationReadRepository,
                NfoDatabaseRegistrationRepository
            >();
            services.AddScoped<
                INfoDatabaseRegistrationWriteRepository,
                NfoDatabaseRegistrationRepository
            >();
            services.AddScoped<
                IMediaDatabaseRegistrationReadRepository,
                MediaDatabaseRegistrationRepository
            >();
            services.AddScoped<
                IMediaDatabaseRegistrationWriteRepository,
                MediaDatabaseRegistrationRepository
            >();
            services.AddScoped<IReleaseCollectionInfoRepository, ReleaseCollectionInfoRepository>();
            services.AddScoped<IMediaMetadataResolverRepository, MediaMetadataResolverRepository>();
        }
    }
}
