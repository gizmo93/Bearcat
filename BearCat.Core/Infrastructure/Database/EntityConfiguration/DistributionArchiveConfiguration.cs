using BearCat.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BearCat.Core.Infrastructure.Database.EntityConfiguration;

public class DistributionArchiveConfiguration : IEntityTypeConfiguration<DistributionArchive>
{
    public void Configure(EntityTypeBuilder<DistributionArchive> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.DistributionId).IsRequired();
        builder.Property(a => a.ArchiveFilePaths).IsRequired();
        builder.Property(a => a.ArchiveUploadId).IsRequired(false);

        builder.HasOne(a => a.ArchiveUpload)
            .WithMany(a => a.DistributionArchives)
            .HasForeignKey(a => a.ArchiveUploadId)
            .HasPrincipalKey(a => a.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
