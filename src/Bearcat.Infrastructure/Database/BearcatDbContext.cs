using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database;

public sealed class BearcatDbContext : DbContext, IBearcatReadDbContext, IBearcatWriteDbContext
{
    public BearcatDbContext(DbContextOptions<BearcatDbContext> options)
        : base(options) { }

    public DbSet<HosterRegistration> HosterRegistrations { get; set; } = null!;

    public DbSet<ImageHosterRegistration> ImageHosterRegistrations { get; set; } = null!;

    public DbSet<ImageUploadConfig> ImageUploadConfigs { get; set; } = null!;

    public DbSet<ImageUpload> ImageUploads { get; set; } = null!;

    public DbSet<ImageUploadUrl> ImageUploadUrls { get; set; } = null!;

    public DbSet<ImageUploadConfigTemplate> ImageUploadConfigTemplates { get; set; } = null!;

    public DbSet<BackgroundTaskState> BackgroundTaskStates { get; set; } = null!;

    public DbSet<Release> Releases { get; set; } = null!;

    public DbSet<ReleaseCollection> ReleaseCollections { get; set; } = null!;

    public DbSet<CollectionUploadSlot> CollectionUploadSlots { get; set; } = null!;

    public DbSet<ReleaseTemplate> ReleaseTemplates { get; set; } = null!;

    public DbSet<ForumPostTemplate> ForumPostTemplates { get; set; } = null!;

    public DbSet<ReleaseFolderAutomation> ReleaseFolderAutomations { get; set; } = null!;

    public DbSet<ArchiveConfigTemplate> ArchiveConfigTemplates { get; set; } = null!;

    public DbSet<UploadConfigTemplate> UploadConfigTemplates { get; set; } = null!;

    public DbSet<UploadConfigLinkCrypterTemplate> UploadConfigLinkCrypterTemplates { get; set; } =
        null!;

    public DbSet<ReleaseGroup> ReleaseGroups { get; set; } = null!;

    public DbSet<Archive> Archives { get; set; } = null!;

    public DbSet<ArchiveConfig> ArchiveConfigs { get; set; } = null!;

    public DbSet<ArchiveFile> ArchiveFiles { get; set; } = null!;

    public DbSet<Upload> Uploads { get; set; } = null!;

    public DbSet<UploadConfig> UploadConfigs { get; set; } = null!;

    public DbSet<UploadedFile> UploadedFiles { get; set; } = null!;

    public DbSet<Notification> Notifications { get; set; } = null!;

    public DbSet<LinkCrypterRegistration> LinkCrypterRegistrations { get; set; } = null!;

    public DbSet<UploadConfigLinkCrypter> UploadConfigLinkCrypters { get; set; } = null!;

    public DbSet<LinkCrypterContainer> LinkCrypterContainers { get; set; } = null!;

    public DbSet<ApplicationConfigurationOverride> ApplicationConfigurationOverrides { get; set; } =
        null!;

    public DbSet<NfoDatabaseRegistration> NfoDatabaseRegistrations { get; set; } = null!;

    public DbSet<ReleaseInfo> ReleaseInfos { get; set; } = null!;

    public DbSet<ReleaseNfo> ReleaseNfos { get; set; } = null!;

    public DbSet<ReleaseExternalInfo> ReleaseExternalInfos { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BearcatDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
