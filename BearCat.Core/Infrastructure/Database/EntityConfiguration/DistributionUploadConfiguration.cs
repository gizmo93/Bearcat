using BearCat.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BearCat.Core.Infrastructure.Database.EntityConfiguration;

public class DistributionUploadConfiguration : IEntityTypeConfiguration<DistributionUpload>
{
    public void Configure(EntityTypeBuilder<DistributionUpload> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.DistributionId).IsRequired();
        builder.Property(u => u.State).IsRequired();
        builder.Property(u => u.CreatedAt).IsRequired().HasPrecision(4);
        builder.Property(u => u.UpdatedAt).IsRequired().HasPrecision(4);

        builder.HasMany(u => u.HosterFiles)
            .WithOne(h => h.DistributionUpload)
            .HasForeignKey(h => h.DistributionUploadId)
            .HasPrincipalKey(u => u.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.ClientCascade);
    }
}
