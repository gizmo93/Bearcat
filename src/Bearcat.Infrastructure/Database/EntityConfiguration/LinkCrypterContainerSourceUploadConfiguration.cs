using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class LinkCrypterContainerSourceUploadConfiguration
    : IEntityTypeConfiguration<LinkCrypterContainerSourceUpload>
{
    public void Configure(EntityTypeBuilder<LinkCrypterContainerSourceUpload> builder)
    {
        builder.HasKey(source => new { source.LinkCrypterContainerId, source.UploadId });

        builder
            .HasOne(source => source.LinkCrypterContainer)
            .WithMany(container => container.SourceUploads)
            .HasForeignKey(source => source.LinkCrypterContainerId)
            .HasPrincipalKey(container => container.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(source => source.Upload)
            .WithMany()
            .HasForeignKey(source => source.UploadId)
            .HasPrincipalKey(upload => upload.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
