using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class UploadedFileConfiguration : IEntityTypeConfiguration<UploadedFile>
{
    public void Configure(EntityTypeBuilder<UploadedFile> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.UploadId).IsRequired();
        builder.Property(u => u.ArchiveFileId).IsRequired();
        builder.Property(u => u.HosterFileLink).IsRequired().HasMaxLength(500);
        builder.Property(u => u.OnlineState).IsRequired();
        builder.Property(u => u.CreatedAt).IsRequired().HasPrecision(4);
        builder.Property(u => u.CheckedAt).IsRequired(false).HasPrecision(4);
        builder.Property(u => u.ErrorMessages).IsRequired();
    }
}
