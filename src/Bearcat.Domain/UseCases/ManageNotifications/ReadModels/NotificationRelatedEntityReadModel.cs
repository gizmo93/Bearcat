namespace Bearcat.Domain.UseCases.ManageNotifications.ReadModels;

public record NotificationRelatedEntityReadModel(
    string EntityType,
    string DisplayName,
    string? TargetUrl
);
