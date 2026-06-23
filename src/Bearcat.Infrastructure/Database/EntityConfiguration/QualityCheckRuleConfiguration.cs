using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class QualityCheckRuleConfiguration : IEntityTypeConfiguration<QualityCheckRule>
{
    public void Configure(EntityTypeBuilder<QualityCheckRule> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.QualityProfileId).IsRequired();
        builder.Property(r => r.RuleType).IsRequired();
        builder.Property(r => r.ParametersJson).IsRequired();
    }
}
