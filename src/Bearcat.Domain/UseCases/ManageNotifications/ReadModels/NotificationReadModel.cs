using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageNotifications.ReadModels;

public record NotificationReadModel(
    int NotificationId,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    NotificationSeverity NotificationSeverity,
    NotificationKind NotificationKind,
    string Message,
    NotificationRelatedEntityReadModel? RelatedEntity
);
