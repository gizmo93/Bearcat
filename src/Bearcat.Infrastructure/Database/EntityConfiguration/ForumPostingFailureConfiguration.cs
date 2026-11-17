using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ForumPostingFailureConfiguration : IEntityTypeConfiguration<ForumPostingFailure>
{
    public void Configure(EntityTypeBuilder<ForumPostingFailure> builder)
    {
        builder.HasKey(failure => failure.Id);
        builder.Property(failure => failure.Id);
        builder.Property(failure => failure.ReleaseId).IsRequired(false);
        builder.Property(failure => failure.ReleaseCollectionId).IsRequired(false);
        builder.Property(failure => failure.DistributionSiteRegistrationId).IsRequired();
        builder.Property(failure => failure.FailedAttemptCount).IsRequired();
        builder.Property(failure => failure.NextAttemptAt).HasPrecision(4).IsRequired(false);
        builder.Property(failure => failure.LastErrorMessage).HasMaxLength(2000).IsRequired();
        builder.Property(failure => failure.State).IsRequired();

        builder
            .HasIndex(failure => new { failure.ReleaseId, failure.DistributionSiteRegistrationId })
            .IsUnique()
            .HasFilter($"\"{nameof(ForumPostingFailure.ReleaseId)}\" IS NOT NULL");

        builder
            .HasIndex(failure => new
            {
                failure.ReleaseCollectionId,
                failure.DistributionSiteRegistrationId,
            })
            .IsUnique()
            .HasFilter($"\"{nameof(ForumPostingFailure.ReleaseCollectionId)}\" IS NOT NULL");

        builder
            .HasOne(failure => failure.Release)
            .WithMany()
            .HasForeignKey(failure => failure.ReleaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(failure => failure.ReleaseCollection)
            .WithMany()
            .HasForeignKey(failure => failure.ReleaseCollectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(failure => failure.DistributionSiteRegistration)
            .WithMany()
            .HasForeignKey(failure => failure.DistributionSiteRegistrationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_ForumPostingFailure_Owner",
                $"(\"{nameof(ForumPostingFailure.ReleaseId)}\" IS NOT NULL) <> (\"{nameof(ForumPostingFailure.ReleaseCollectionId)}\" IS NOT NULL)"
            )
        );
    }
}
