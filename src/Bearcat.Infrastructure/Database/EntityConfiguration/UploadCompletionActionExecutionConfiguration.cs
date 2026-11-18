using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class UploadCompletionActionExecutionConfiguration
    : IEntityTypeConfiguration<UploadCompletionActionExecution>
{
    public void Configure(EntityTypeBuilder<UploadCompletionActionExecution> builder)
    {
        builder.HasKey(execution => execution.Id);
        builder.Property(execution => execution.Id).IsRequired();
        builder.Property(execution => execution.ReleaseId).IsRequired(false);
        builder.Property(execution => execution.ReleaseCollectionId).IsRequired(false);
        builder.Property(execution => execution.UploadCompletionActionRegistrationId).IsRequired();
        builder.Property(execution => execution.HandledUploadedAt).IsRequired().HasPrecision(4);
        builder.Property(execution => execution.ExecutedAt).IsRequired().HasPrecision(4);
        builder.Property(execution => execution.ResultText).IsRequired(false).HasMaxLength(2000);
        builder.Property(execution => execution.ErrorMessage).IsRequired(false).HasMaxLength(2000);

        builder
            .HasIndex(execution => new
            {
                execution.ReleaseId,
                execution.UploadCompletionActionRegistrationId,
            })
            .IsUnique();

        builder
            .HasIndex(execution => new
            {
                execution.ReleaseCollectionId,
                execution.UploadCompletionActionRegistrationId,
            })
            .IsUnique();

        builder
            .HasOne(execution => execution.Release)
            .WithMany()
            .HasForeignKey(execution => execution.ReleaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(execution => execution.ReleaseCollection)
            .WithMany()
            .HasForeignKey(execution => execution.ReleaseCollectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(execution => execution.UploadCompletionActionRegistration)
            .WithMany()
            .HasForeignKey(execution => execution.UploadCompletionActionRegistrationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_UploadCompletionActionExecution_Owner",
                $"(\"{nameof(UploadCompletionActionExecution.ReleaseId)}\" IS NOT NULL) <> (\"{nameof(UploadCompletionActionExecution.ReleaseCollectionId)}\" IS NOT NULL)"
            )
        );
    }
}
