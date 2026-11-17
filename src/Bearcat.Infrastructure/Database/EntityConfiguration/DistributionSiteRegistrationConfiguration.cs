using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class DistributionSiteRegistrationConfiguration
    : IEntityTypeConfiguration<DistributionSiteRegistration>
{
    public void Configure(EntityTypeBuilder<DistributionSiteRegistration> builder)
    {
        builder.HasKey(registration => registration.Id);
        builder.Property(registration => registration.Id);
        builder.Property(registration => registration.Name).HasMaxLength(100).IsRequired();
        builder
            .Property(registration => registration.DistributionSiteClassName)
            .HasMaxLength(100)
            .IsRequired();
        builder
            .Property(registration => registration.SerializedConfig)
            .HasMaxLength(4000)
            .IsRequired();
        builder.Property(registration => registration.IsActive).IsRequired();
        builder.Property(registration => registration.EnableAutomaticPosting).IsRequired();
        builder.Property(registration => registration.StripDotsForThreadSearch).IsRequired();
        builder.Property(registration => registration.RequiredHosterClassNames).IsRequired();
        builder.Property(registration => registration.ForbiddenHosterClassNames).IsRequired();
        builder.Property(registration => registration.RequiredLinkCrypterClassNames).IsRequired();
        builder.Property(registration => registration.ForbiddenLinkCrypterClassNames).IsRequired();
        builder.Property(registration => registration.RequiredImageHosterClassNames).IsRequired();
        builder.Property(registration => registration.ForbiddenImageHosterClassNames).IsRequired();
        builder.Property(registration => registration.EncryptedSession).HasMaxLength(8000);
        builder.Property(registration => registration.HasUnreadableSecrets).IsRequired();

        builder
            .HasMany(registration => registration.PostingRules)
            .WithOne(rule => rule.DistributionSiteRegistration)
            .HasForeignKey(rule => rule.DistributionSiteRegistrationId)
            .HasPrincipalKey(registration => registration.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
