using Bearcat.Domain.Entities;
using Bearcat.Infrastructure.Database.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database;

public class BearcatDbContext(DbContextOptions<BearcatDbContext> options)
    : DbContext(options), IBearcatReadDbContext, IBearcatWriteDbContext
{
    public DbSet<HosterRegistration> HosterRegistrations { get; set; } = null!;

    public DbSet<Release> Releases { get; set; } = null!;

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


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BearcatDbContext).Assembly);

        modelBuilder.AddNotificationEntity<Upload, UploadNotification>(
            u => u.Notifications,
            n => n.UploadNotification);

        modelBuilder.AddNotificationEntity<Archive, ArchiveNotification>(
            a => a.Notifications,
            n => n.ArchiveNotification);

        modelBuilder.AddNotificationEntity<LinkCrypterContainer, LinkCrypterContainerNotification>(
            lc => lc.Notifications,
            n => n.LinkCrypterContainerNotification);

        base.OnModelCreating(modelBuilder);
    }
}
