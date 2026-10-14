using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class HosterAccountPoolMemberConfiguration
    : IEntityTypeConfiguration<HosterAccountPoolMember>
{
    public void Configure(EntityTypeBuilder<HosterAccountPoolMember> builder)
    {
        builder.HasKey(member => member.Id);
        builder.Property(member => member.Id).IsRequired();
        builder.Property(member => member.HosterAccountPoolId).IsRequired();
        builder.Property(member => member.HosterRegistrationId).IsRequired();
        builder.Property(member => member.Priority).IsRequired();

        builder
            .HasIndex(member => new { member.HosterAccountPoolId, member.HosterRegistrationId })
            .IsUnique();

        builder
            .HasOne(member => member.HosterRegistration)
            .WithMany(registration => registration.HosterAccountPoolMemberships)
            .HasForeignKey(member => member.HosterRegistrationId)
            .HasPrincipalKey(registration => registration.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
