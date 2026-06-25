using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ReleaseClassificationConfiguration : IEntityTypeConfiguration<ReleaseClassification>
{
    public void Configure(EntityTypeBuilder<ReleaseClassification> builder)
    {
        builder.HasKey(classification => classification.Id);

        builder.Property(classification => classification.ReleaseId).IsRequired();
        builder.Property(classification => classification.Title).IsRequired().HasMaxLength(500);
        builder.Property(classification => classification.Year).IsRequired(false);
        builder.Property(classification => classification.Season).IsRequired(false);
        builder.Property(classification => classification.Episode).IsRequired(false);
        builder.Property(classification => classification.Resolution).IsRequired();
        builder.Property(classification => classification.ResolutionSource).IsRequired();
        builder
            .Property(classification => classification.PrimaryLanguage)
            .IsRequired(false)
            .HasMaxLength(100);
        builder.Property(classification => classification.LanguageSource).IsRequired();
        builder.Property(classification => classification.IsMultiLanguage).IsRequired();
        builder.Property(classification => classification.ParserVersion).IsRequired();
        builder.Property(classification => classification.ClassifiedAt).IsRequired().HasPrecision(4);

        builder.HasIndex(classification => classification.ReleaseId).IsUnique();
    }
}
