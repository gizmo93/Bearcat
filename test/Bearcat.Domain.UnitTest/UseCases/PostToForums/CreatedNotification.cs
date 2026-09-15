using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UnitTest.UseCases.PostToForums;

public sealed record CreatedNotification(NotificationKind Kind, string Message);
