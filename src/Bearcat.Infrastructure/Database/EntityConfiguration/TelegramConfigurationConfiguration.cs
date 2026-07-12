using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class TelegramConfigurationConfiguration : IEntityTypeConfiguration<TelegramConfiguration>
{
    public void Configure(EntityTypeBuilder<TelegramConfiguration> builder)
    {
        builder.HasKey(configuration => configuration.Id);
        builder.Property(configuration => configuration.EncryptedBotToken).IsRequired();
        builder.Property(configuration => configuration.BotUsername).HasMaxLength(64).IsRequired();
        builder
            .Property(configuration => configuration.NotificationBaseUrl)
            .HasMaxLength(2000)
            .IsRequired();
        builder.Property(configuration => configuration.ChatId);
        builder.Property(configuration => configuration.ChatName).HasMaxLength(200);
        builder.Property(configuration => configuration.ForwardInfo).IsRequired();
        builder.Property(configuration => configuration.ForwardWarning).IsRequired();
        builder.Property(configuration => configuration.ForwardError).IsRequired();
        builder.Property(configuration => configuration.PairingTokenHash).HasMaxLength(64);
        builder.Property(configuration => configuration.PairingExpiresAt).HasPrecision(4);
        builder.Property(configuration => configuration.UpdateOffset).IsRequired();
        builder.Property(configuration => configuration.ForwardNotificationsAfterId).IsRequired();
    }
}
