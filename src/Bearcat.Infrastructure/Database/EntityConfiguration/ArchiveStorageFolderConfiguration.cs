using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ArchiveStorageFolderConfiguration : IEntityTypeConfiguration<ArchiveStorageFolder>
{
    public void Configure(EntityTypeBuilder<ArchiveStorageFolder> builder)
    {
        builder.HasKey(storageFolder => storageFolder.Id);
        builder.Property(storageFolder => storageFolder.Id).IsRequired();
        builder.Property(storageFolder => storageFolder.Name).IsRequired().HasMaxLength(100);
        builder.Property(storageFolder => storageFolder.Path).IsRequired().HasMaxLength(500);
        builder.Property(storageFolder => storageFolder.IsActive).IsRequired();
        builder.Property(storageFolder => storageFolder.MinimumFreeSpaceGb).IsRequired();
        builder.Property(storageFolder => storageFolder.Priority).IsRequired();
        builder
            .Property(storageFolder => storageFolder.UseLocalWorkingCopyForReuploads)
            .IsRequired();

        builder.HasIndex(storageFolder => storageFolder.Name).IsUnique();
        builder.HasIndex(storageFolder => storageFolder.Path).IsUnique();
    }
}
