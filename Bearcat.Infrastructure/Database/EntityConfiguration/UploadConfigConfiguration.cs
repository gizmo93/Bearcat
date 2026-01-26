using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class UploadConfigConfiguration : IEntityTypeConfiguration<UploadConfig>
{
    public void Configure(EntityTypeBuilder<UploadConfig> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.HosterRegistrationId).IsRequired();
        builder.Property(u => u.ArchiveConfigId).IsRequired();
        builder.Property(u => u.Name).HasMaxLength(200).IsRequired();
        builder.Property(u => u.LinksDistributedTo);

        builder.HasOne(u => u.HosterRegistration)
            .WithMany(h => h.UploadConfigs)
            .HasForeignKey(u => u.HosterRegistrationId)
            .HasPrincipalKey(h => h.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.Uploads)
            .WithOne(u => u.UploadConfig)
            .HasForeignKey(u => u.UploadConfigId)
            .HasPrincipalKey(u => u.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.LinkCrypters)
            .WithOne(l => l.UploadConfig)
            .HasForeignKey(l => l.UploadConfigId)
            .HasPrincipalKey(u => u.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
