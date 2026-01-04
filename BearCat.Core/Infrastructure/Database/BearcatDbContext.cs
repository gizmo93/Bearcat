using BearCat.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BearCat.Core.Infrastructure.Database;

public class BearcatDbContext(DbContextOptions<BearcatDbContext> options)
    : DbContext(options), IBearcatReadDbContext, IBearcatWriteDbContext
{
    public DbSet<HosterRegistration> HosterRegistrations { get; set; } = null!;
    
    public DbSet<Distribution> Distributions { get; set; } = null!;
    
    public DbSet<DistributionArchive> DistributionArchives { get; set; } = null!;
    
    public DbSet<ArchiveUpload> DistributionUploads { get; set; } = null!;
    
    public DbSet<HosterFile> HosterFiles { get; set; } = null!;
    
    public DbSet<Release> Releases { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BearcatDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
