using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageNotifications.ReadModels;

public record NotificationReadModel(
    int NotificationId,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    NotificationType NotificationType,
    string Message,
    NotificationRelatedEntityReadModel? RelatedEntity
);
