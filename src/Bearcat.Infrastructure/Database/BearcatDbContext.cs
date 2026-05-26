using Bearcat.Domain.Entities;
using Bearcat.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Bearcat.Infrastructure.Database;

public class BearcatDbContext : DbContext, IBearcatReadDbContext, IBearcatWriteDbContext
{
    private readonly ISecretProtector secretProtector;

    public BearcatDbContext(
        DbContextOptions<BearcatDbContext> options,
        ISecretProtector? secretProtector = null
    )
        : base(options)
    {
        this.secretProtector = secretProtector ?? NoOpSecretProtector.Instance;
        ChangeTracker.Tracked += (_, eventArgs) =>
        {
            if (eventArgs.FromQuery)
            {
                UnprotectTrackedRegistrationConfiguration(eventArgs.Entry);
            }
        };
    }

    public DbSet<HosterRegistration> HosterRegistrations { get; set; } = null!;

    public DbSet<BackgroundTaskState> BackgroundTaskStates { get; set; } = null!;

    public DbSet<Release> Releases { get; set; } = null!;

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

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ProtectRegistrationConfigurations();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ProtectRegistrationConfigurations()
    {
        foreach (var entry in ChangeTracker.Entries<HosterRegistration>())
        {
            ProtectAddedOrModifiedProperty(entry, registration => registration.SerializedConfig);
        }

        foreach (var entry in ChangeTracker.Entries<LinkCrypterRegistration>())
        {
            ProtectAddedOrModifiedProperty(entry, registration => registration.SerializedConfig);
        }

        foreach (var entry in ChangeTracker.Entries<NfoDatabaseRegistration>())
        {
            ProtectAddedOrModifiedProperty(entry, registration => registration.SerializedConfig);
        }
    }

    private void UnprotectTrackedRegistrationConfiguration(EntityEntry entry)
    {
        switch (entry.Entity)
        {
            case HosterRegistration registration:
                UnprotectProperty(
                    entry,
                    registration.SerializedConfig,
                    value => registration.SerializedConfig = value
                );
                break;
            case LinkCrypterRegistration registration:
                UnprotectProperty(
                    entry,
                    registration.SerializedConfig,
                    value => registration.SerializedConfig = value
                );
                break;
            case NfoDatabaseRegistration registration:
                UnprotectProperty(
                    entry,
                    registration.SerializedConfig,
                    value => registration.SerializedConfig = value
                );
                break;
        }
    }

    private void ProtectAddedOrModifiedProperty<TEntity>(
        EntityEntry<TEntity> entry,
        Func<TEntity, string> propertySelector
    )
        where TEntity : class
    {
        if (
            entry.State != EntityState.Added
            && !entry.Property(nameof(HosterRegistration.SerializedConfig)).IsModified
        )
        {
            return;
        }

        entry.Property(nameof(HosterRegistration.SerializedConfig)).CurrentValue =
            secretProtector.Protect(propertySelector(entry.Entity));
    }

    private void UnprotectProperty(EntityEntry entry, string currentValue, Action<string> setValue)
    {
        var unprotectedValue = secretProtector.Unprotect(currentValue);
        if (unprotectedValue == currentValue)
        {
            return;
        }

        setValue(unprotectedValue);

        var property = entry.Property(nameof(HosterRegistration.SerializedConfig));
        property.OriginalValue = unprotectedValue;
        property.IsModified = false;
    }
}
