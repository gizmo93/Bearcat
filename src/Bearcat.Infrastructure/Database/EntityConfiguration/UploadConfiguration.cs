using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class UploadConfiguration : IEntityTypeConfiguration<Upload>
{
    public void Configure(EntityTypeBuilder<Upload> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.UploadConfigId).IsRequired();
        builder.Property(u => u.ArchiveId).IsRequired(false);
        builder.Property(u => u.CreatedAt).IsRequired().HasPrecision(4);
        builder.Property(u => u.UploadedAt).IsRequired(false).HasPrecision(4);
        builder.Property(u => u.UploadState).IsRequired();
        builder.Property(u => u.OnlineState).IsRequired();
        builder.Property(u => u.PremiumOnlyDownload).IsRequired();
        builder.Property(u => u.ErrorMessages);

        builder.HasIndex(u => new { u.UploadState, u.OnlineState });

        builder
            .HasMany(u => u.UploadedFiles)
            .WithOne(u => u.Upload)
            .HasForeignKey(u => u.UploadId)
            .HasPrincipalKey(u => u.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(u => u.LinkCrypterContainers)
            .WithOne(l => l.Upload)
            .HasForeignKey(l => l.UploadId)
            .HasPrincipalKey(u => u.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
