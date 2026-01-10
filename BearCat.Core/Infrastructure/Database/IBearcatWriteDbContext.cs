using BearCat.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace BearCat.Core.Infrastructure.Database;

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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    EntityEntry Add(object entity);

    EntityEntry Remove(object entity);
}
