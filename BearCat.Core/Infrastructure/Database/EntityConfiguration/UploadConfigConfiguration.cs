using BearCat.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BearCat.Core.Infrastructure.Database.EntityConfiguration;

public class UploadConfigConfiguration : IEntityTypeConfiguration<UploadConfig>
{
    public void Configure(EntityTypeBuilder<UploadConfig> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.HosterRegistrationId).IsRequired();
        builder.Property(u => u.ArchiveConfigId).IsRequired();
        builder.Property(u => u.Name).HasMaxLength(200).IsRequired();

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
    }
}
