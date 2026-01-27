using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Extensions;

public static class ModelBuilderExtensions
{
    extension(ModelBuilder builder)
    {
        public void AddNotificationEntity<TEntity, TNotificationEntity>()
            where TEntity : class
            where TNotificationEntity : EntityNotification<TEntity>
        {
            builder.Entity<TNotificationEntity>(b =>
            {
                b.ToTable($"{typeof(TEntity).Name}Notifications");
                
                b.HasKey(e => e.NotificationId);
                b.Property(e => e.EntityId).IsRequired();
                b.Property(e => e.NotificationId).IsRequired();

                b.HasOne<TEntity>(n => n.Entity)
                    .WithMany()
                    .HasForeignKey(n => n.EntityId)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne<Notification>(n => n.Notification)
                    .WithOne()
                    .HasForeignKey<TNotificationEntity>(n => n.NotificationId)
                    .HasPrincipalKey<Notification>(n => n.Id)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
