using System.Linq.Expressions;
using Bearcat.Domain.Abstractions;
using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Extensions;

public static class ModelBuilderExtensions
{
    extension(ModelBuilder builder)
    {
        public void AddNotificationEntity<TEntity, TNotificationEntity>(
            Expression<Func<TEntity, IEnumerable<TNotificationEntity>?>>? entitySelector,
            Expression<Func<Notification, TNotificationEntity?>>? notificationSelector)
            where TEntity : class, IEntityWithNotifications
            where TNotificationEntity : EntityNotification<TEntity>
        {
            builder.Entity<TNotificationEntity>(b =>
            {
                b.ToTable($"{typeof(TEntity).Name}Notifications");

                b.HasKey(e => e.NotificationId);
                b.Property(e => e.EntityId).IsRequired();
                b.Property(e => e.NotificationId).IsRequired();

                b.HasOne<TEntity>(n => n.Entity)
                    .WithMany(entitySelector)
                    .HasForeignKey(n => n.EntityId)
                    .HasPrincipalKey(e => e.Id)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne<Notification>(n => n.Notification)
                    .WithOne(notificationSelector)
                    .HasForeignKey<TNotificationEntity>(n => n.NotificationId)
                    .HasPrincipalKey<Notification>(n => n.Id)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
