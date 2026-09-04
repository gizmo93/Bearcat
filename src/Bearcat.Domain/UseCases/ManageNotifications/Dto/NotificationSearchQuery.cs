using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageNotifications.Dto;

public record NotificationSearchQuery(
    int PageIndex = 0,
    int PageSize = 10,
    bool IncludeResolved = false,
    NotificationKind? NotificationKind = null
);
