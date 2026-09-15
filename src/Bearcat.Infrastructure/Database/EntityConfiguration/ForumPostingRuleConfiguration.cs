using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ForumPostingRuleConfiguration : IEntityTypeConfiguration<ForumPostingRule>
{
    public void Configure(EntityTypeBuilder<ForumPostingRule> builder)
    {
        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.Id);
        builder.Property(rule => rule.DistributionSiteRegistrationId).IsRequired();
        builder.Property(rule => rule.SortOrder).IsRequired();
        builder.Property(rule => rule.Name).IsRequired().HasMaxLength(200);
        builder.Property(rule => rule.ConditionJson).IsRequired().HasMaxLength(8000);
        builder.Property(rule => rule.TargetNodeId).IsRequired().HasMaxLength(100);
        builder.Property(rule => rule.TargetPathSnapshot).IsRequired().HasMaxLength(500);
        builder.Property(rule => rule.ThreadPrefixId).IsRequired(false).HasMaxLength(100);
        builder.Property(rule => rule.ForumPostTemplateId).IsRequired();
        builder.Property(rule => rule.PostMode).IsRequired();
        builder.Property(rule => rule.IsEnabled).IsRequired();
        builder.Property(rule => rule.CreatedAt).IsRequired().HasPrecision(4);
        builder.Property(rule => rule.UpdatedAt).IsRequired().HasPrecision(4);

        builder
            .HasOne(rule => rule.ForumPostTemplate)
            .WithMany()
            .HasForeignKey(rule => rule.ForumPostTemplateId)
            .HasPrincipalKey(template => template.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(rule => new { rule.DistributionSiteRegistrationId, rule.SortOrder });
    }
}
