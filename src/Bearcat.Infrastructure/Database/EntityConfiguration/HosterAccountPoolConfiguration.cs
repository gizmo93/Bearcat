using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class HosterAccountPoolConfiguration : IEntityTypeConfiguration<HosterAccountPool>
{
    public void Configure(EntityTypeBuilder<HosterAccountPool> builder)
    {
        builder.HasKey(pool => pool.Id);
        builder.Property(pool => pool.Id).IsRequired();
        builder.Property(pool => pool.Name).IsRequired().HasMaxLength(200);
        builder.Property(pool => pool.HosterClassName).IsRequired().HasMaxLength(500);
        builder.Property(pool => pool.MinimumFreeSpaceBytes).IsRequired().HasDefaultValue(0L);

        builder.HasIndex(pool => pool.Name).IsUnique();

        builder
            .HasMany(pool => pool.Members)
            .WithOne(member => member.HosterAccountPool)
            .HasForeignKey(member => member.HosterAccountPoolId)
            .HasPrincipalKey(pool => pool.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
