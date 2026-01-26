using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Bearcat.Infrastructure.Database;

public interface IBearcatWriteDbContext
{
    public DbSet<HosterRegistration> HosterRegistrations { get; set; }
    public DbSet<Release> Releases { get; set; }
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
}
