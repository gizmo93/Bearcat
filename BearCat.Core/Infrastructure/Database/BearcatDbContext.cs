using BearCat.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BearCat.Core.Infrastructure.Database;

public class BearcatDbContext(DbContextOptions<BearcatDbContext> options)
    : DbContext(options), IBearcatReadDbContext, IBearcatWriteDbContext
{
    public DbSet<HosterRegistration> HosterRegistrations { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BearcatDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
