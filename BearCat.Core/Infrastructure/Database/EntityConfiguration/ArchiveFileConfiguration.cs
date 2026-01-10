using BearCat.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BearCat.Core.Infrastructure.Database.EntityConfiguration;

public class ArchiveFileConfiguration : IEntityTypeConfiguration<ArchiveFile>
{
    public void Configure(EntityTypeBuilder<ArchiveFile> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.ArchiveId).IsRequired();
        builder.Property(a => a.FullFileName).IsRequired().HasMaxLength(1000);

        builder.HasMany(a => a.UploadedFiles)
            .WithOne(u => u.ArchiveFile)
            .HasForeignKey(u => u.ArchiveFileId)
            .HasPrincipalKey(a => a.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
