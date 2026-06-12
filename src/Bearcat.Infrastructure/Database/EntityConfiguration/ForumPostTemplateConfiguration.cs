using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ForumPostTemplateConfiguration : IEntityTypeConfiguration<ForumPostTemplate>
{
    public void Configure(EntityTypeBuilder<ForumPostTemplate> builder)
    {
        builder.HasKey(template => template.Id);
        builder.Property(template => template.Name).IsRequired().HasMaxLength(200);
        builder
            .Property(template => template.Type)
            .IsRequired()
            .HasDefaultValue(ForumPostTemplateType.Release);
        builder.Property(template => template.TemplateBody).IsRequired();
        builder.Property(template => template.CreatedAt).IsRequired().HasPrecision(4);
        builder.Property(template => template.UpdatedAt).IsRequired().HasPrecision(4);

        builder.HasIndex(template => template.Name).IsUnique();
    }
}
