using BearCat.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace BearCat.Core.Infrastructure.Database;

public interface IBearcatWriteDbContext
{
    DbSet<HosterRegistration> HosterRegistrations { get; set; }
    DbSet<Distribution> Distributions { get; set; }
    DbSet<ArchiveUpload> ArchiveUploads { get; set; }
    DbSet<HosterFile> HosterFiles { get; set; }
    DbSet<Release> Releases { get; set; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    EntityEntry Add(object entity);

    EntityEntry Remove(object entity);
}
