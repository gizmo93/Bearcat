using BearCat.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BearCat.Core.Infrastructure.Database;

public interface IBearcatReadDbContext
{
    public DbSet<HosterRegistration> HosterRegistrations { get; set; }
    public DbSet<Distribution> Distributions { get; set; }
    public DbSet<ArchiveUpload> ArchiveUploads { get; set; }
    public DbSet<HosterFile> HosterFiles { get; set; }
    public DbSet<Release> Releases { get; set; }
}
