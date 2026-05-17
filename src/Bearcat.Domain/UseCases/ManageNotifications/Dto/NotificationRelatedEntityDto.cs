namespace Bearcat.Domain.UseCases.ManageNotifications.Dto;

public record NotificationRelatedEntityDto(
    string EntityType,
    string DisplayName,
    string? TargetUrl
);
