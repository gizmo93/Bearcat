using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database;

public interface IBearcatReadDbContext
{
    public DbSet<HosterRegistration> HosterRegistrations { get; set; }
    public DbSet<BackgroundTaskState> BackgroundTaskStates { get; set; }
    public DbSet<Release> Releases { get; set; }
    public DbSet<ReleaseTemplate> ReleaseTemplates { get; set; }
    public DbSet<ForumPostTemplate> ForumPostTemplates { get; set; }
    public DbSet<ReleaseFolderAutomation> ReleaseFolderAutomations { get; set; }
    public DbSet<ArchiveConfigTemplate> ArchiveConfigTemplates { get; set; }
    public DbSet<UploadConfigTemplate> UploadConfigTemplates { get; set; }
    public DbSet<UploadConfigLinkCrypterTemplate> UploadConfigLinkCrypterTemplates { get; set; }
    public DbSet<ReleaseGroup> ReleaseGroups { get; set; }
    public DbSet<Archive> Archives { get; set; }
    public DbSet<ArchiveConfig> ArchiveConfigs { get; set; }
    public DbSet<ArchiveFile> ArchiveFiles { get; set; }
    public DbSet<Upload> Uploads { get; set; }
    public DbSet<UploadConfig> UploadConfigs { get; set; }
    public DbSet<UploadedFile> UploadedFiles { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<LinkCrypterRegistration> LinkCrypterRegistrations { get; set; }
    public DbSet<UploadConfigLinkCrypter> UploadConfigLinkCrypters { get; set; }
    public DbSet<LinkCrypterContainer> LinkCrypterContainers { get; set; }
    public DbSet<ApplicationConfigurationOverride> ApplicationConfigurationOverrides { get; set; }
    public DbSet<NfoDatabaseRegistration> NfoDatabaseRegistrations { get; set; }
    public DbSet<ReleaseInfo> ReleaseInfos { get; set; }
    public DbSet<ReleaseExternalInfo> ReleaseExternalInfos { get; set; }
}
