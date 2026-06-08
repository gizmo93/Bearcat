using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class CollectionUploadSlotConfiguration : IEntityTypeConfiguration<CollectionUploadSlot>
{
    public void Configure(EntityTypeBuilder<CollectionUploadSlot> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.ReleaseCollectionId).IsRequired();
        builder.Property(s => s.Key).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.IsRequired).IsRequired();
        builder.Property(s => s.PasswordPolicy).IsRequired();
        builder.Property(s => s.ExpectedArchivePassword).IsRequired(false).HasMaxLength(100);

        builder.HasIndex(s => new { s.ReleaseCollectionId, s.Key }).IsUnique();

        builder
            .HasOne(s => s.ReleaseCollection)
            .WithMany(c => c.UploadSlots)
            .HasForeignKey(s => s.ReleaseCollectionId)
            .HasPrincipalKey(c => c.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
