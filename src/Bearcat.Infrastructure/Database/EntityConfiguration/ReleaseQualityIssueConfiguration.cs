using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ReleaseQualityIssueConfiguration : IEntityTypeConfiguration<ReleaseQualityIssue>
{
    public void Configure(EntityTypeBuilder<ReleaseQualityIssue> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.ReleaseId).IsRequired();
        builder.Property(i => i.RuleType).IsRequired();
        builder.Property(i => i.Description).HasMaxLength(1000).IsRequired();
    }
}
