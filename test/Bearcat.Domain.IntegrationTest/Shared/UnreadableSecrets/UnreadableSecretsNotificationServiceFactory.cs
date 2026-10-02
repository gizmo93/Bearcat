using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.UseCases.DetectUnreadableSecrets;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;

namespace Bearcat.Domain.IntegrationTest.Shared.UnreadableSecrets;

public static class UnreadableSecretsNotificationServiceFactory
{
    public static UnreadableSecretsNotificationService Create(
        BearcatDbContext dbContext,
        IApplicationConfigurationProvider configurationProvider
    )
    {
        return new UnreadableSecretsNotificationService(
            repository: new UnreadableSecretsRepository(dbContext),
            notificationService: new NotificationService(
                repository: new NotificationRepository(dbContext),
                timeProvider: new ControllableTimeProvider(
                    new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Unspecified)
                ),
                configurationProvider: configurationProvider
            )
        );
    }
}
