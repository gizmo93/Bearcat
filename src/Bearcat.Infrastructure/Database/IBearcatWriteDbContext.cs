using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Bearcat.Infrastructure.Database;

public interface IBearcatWriteDbContext
{
    public DbSet<HosterRegistration> HosterRegistrations { get; set; }
    public DbSet<ImageHosterRegistration> ImageHosterRegistrations { get; set; }
    public DbSet<DistributionSiteRegistration> DistributionSiteRegistrations { get; set; }
    public DbSet<ImageUploadConfig> ImageUploadConfigs { get; set; }
    public DbSet<ImageUpload> ImageUploads { get; set; }
    public DbSet<ImageUploadUrl> ImageUploadUrls { get; set; }
    public DbSet<ImageUploadConfigTemplate> ImageUploadConfigTemplates { get; set; }
    public DbSet<CollectionImageUploadConfigTemplate> CollectionImageUploadConfigTemplates { get; set; }
    public DbSet<BackgroundTaskState> BackgroundTaskStates { get; set; }
    public DbSet<Release> Releases { get; set; }
    public DbSet<ReleaseCollection> ReleaseCollections { get; set; }
    public DbSet<CollectionUploadSlot> CollectionUploadSlots { get; set; }
    public DbSet<ReleaseTemplate> ReleaseTemplates { get; set; }
    public DbSet<ForumPostTemplate> ForumPostTemplates { get; set; }
    public DbSet<ReleaseFolderAutomation> ReleaseFolderAutomations { get; set; }
    public DbSet<ArchiveConfigTemplate> ArchiveConfigTemplates { get; set; }
    public DbSet<UploadConfigTemplate> UploadConfigTemplates { get; set; }
    public DbSet<UploadConfigLinkCrypterTemplate> UploadConfigLinkCrypterTemplates { get; set; }
    public DbSet<ReleaseGroup> ReleaseGroups { get; set; }
    DbSet<Archive> Archives { get; set; }
    DbSet<ArchiveConfig> ArchiveConfigs { get; set; }
    DbSet<ArchiveFile> ArchiveFiles { get; set; }
    DbSet<Upload> Uploads { get; set; }
    DbSet<UploadConfig> UploadConfigs { get; set; }
    DbSet<UploadedFile> UploadedFiles { get; set; }
    DbSet<Notification> Notifications { get; set; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    EntityEntry Add(object entity);

    EntityEntry Remove(object entity);

    ChangeTracker ChangeTracker { get; }
    DbSet<LinkCrypterRegistration> LinkCrypterRegistrations { get; set; }
    DbSet<UploadConfigLinkCrypter> UploadConfigLinkCrypters { get; set; }
    DbSet<LinkCrypterContainer> LinkCrypterContainers { get; set; }
    DbSet<LinkCrypterContainerSourceUpload> LinkCrypterContainerSourceUploads { get; set; }
    DbSet<ApplicationConfigurationOverride> ApplicationConfigurationOverrides { get; set; }
    DbSet<NfoDatabaseRegistration> NfoDatabaseRegistrations { get; set; }
    DbSet<ReleaseInfo> ReleaseInfos { get; set; }
    DbSet<ReleaseNfo> ReleaseNfos { get; set; }
    DbSet<ReleaseExternalInfo> ReleaseExternalInfos { get; set; }
    DbSet<SeriesDatabaseRegistration> SeriesDatabaseRegistrations { get; set; }
    DbSet<ReleaseCollectionMetadata> ReleaseCollectionMetadata { get; set; }
}
