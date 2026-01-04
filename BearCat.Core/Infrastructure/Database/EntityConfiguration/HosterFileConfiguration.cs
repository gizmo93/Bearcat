using BearCat.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BearCat.Core.Infrastructure.Database.EntityConfiguration;

public class HosterFileConfiguration : IEntityTypeConfiguration<HosterFile>
{
    public void Configure(EntityTypeBuilder<HosterFile> builder)
    {
        builder.HasKey(h => h.Id);
        builder.Property(h => h.DistributionUploadId).IsRequired();
        builder.Property(h => h.SourceFileName).IsRequired().HasMaxLength(200);
        builder.Property(h => h.FileUrl).IsRequired(false).HasMaxLength(200);
        builder.Property(h => h.State).IsRequired();
    }
}
