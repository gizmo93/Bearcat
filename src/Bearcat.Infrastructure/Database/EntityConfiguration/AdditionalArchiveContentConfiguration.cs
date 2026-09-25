using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class AdditionalArchiveContentConfiguration
    : IEntityTypeConfiguration<AdditionalArchiveContent>
{
    public void Configure(EntityTypeBuilder<AdditionalArchiveContent> builder)
    {
        builder.HasKey(content => content.Id);
        builder.Property(content => content.Name).IsRequired().HasMaxLength(200);
        builder.Property(content => content.Type).IsRequired();
        builder.Property(content => content.SourcePath).IsRequired(false).HasMaxLength(1000);
        builder.Property(content => content.FileName).IsRequired(false).HasMaxLength(255);
        builder.Property(content => content.TextContent).IsRequired(false).HasColumnType("text");

        builder.HasIndex(content => content.Name).IsUnique();

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_AdditionalArchiveContent_FieldsMatchType",
                $"(\"{nameof(AdditionalArchiveContent.Type)}\" = {(int)AdditionalArchiveContentType.Path}"
                    + $" AND \"{nameof(AdditionalArchiveContent.SourcePath)}\" IS NOT NULL"
                    + $" AND \"{nameof(AdditionalArchiveContent.FileName)}\" IS NULL"
                    + $" AND \"{nameof(AdditionalArchiveContent.TextContent)}\" IS NULL)"
                    + $" OR (\"{nameof(AdditionalArchiveContent.Type)}\" = {(int)AdditionalArchiveContentType.TextFile}"
                    + $" AND \"{nameof(AdditionalArchiveContent.SourcePath)}\" IS NULL"
                    + $" AND \"{nameof(AdditionalArchiveContent.FileName)}\" IS NOT NULL"
                    + $" AND \"{nameof(AdditionalArchiveContent.TextContent)}\" IS NOT NULL)"
            )
        );
    }
}
