using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageNotifications.Dto;

public record NotificationDto(
    int NotificationId,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    NotificationType NotificationType,
    string Message,
    NotificationRelatedEntityDto? RelatedEntity
);
