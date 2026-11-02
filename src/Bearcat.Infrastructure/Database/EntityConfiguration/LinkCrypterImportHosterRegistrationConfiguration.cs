using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class LinkCrypterImportHosterRegistrationConfiguration
    : IEntityTypeConfiguration<LinkCrypterImportHosterRegistration>
{
    public void Configure(EntityTypeBuilder<LinkCrypterImportHosterRegistration> builder)
    {
        builder.HasKey(hosterRegistration => hosterRegistration.Id);
        builder.Property(hosterRegistration => hosterRegistration.Id).IsRequired();
        builder.Property(hosterRegistration => hosterRegistration.LinkCrypterImportId).IsRequired();
        builder
            .Property(hosterRegistration => hosterRegistration.HosterClassName)
            .IsRequired()
            .HasMaxLength(500);
        builder
            .Property(hosterRegistration => hosterRegistration.HosterRegistrationId)
            .IsRequired(false);

        builder
            .HasOne(hosterRegistration => hosterRegistration.HosterRegistration)
            .WithMany()
            .HasForeignKey(hosterRegistration => hosterRegistration.HosterRegistrationId)
            .HasPrincipalKey(registration => registration.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasIndex(hosterRegistration => new
            {
                hosterRegistration.LinkCrypterImportId,
                hosterRegistration.HosterClassName,
            })
            .IsUnique();
        builder.HasIndex(hosterRegistration => hosterRegistration.HosterRegistrationId);
    }
}
