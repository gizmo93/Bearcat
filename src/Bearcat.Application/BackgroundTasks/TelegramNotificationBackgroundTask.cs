using Bearcat.Domain.UseCases.ManageNotifications.Telegram;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public sealed class TelegramNotificationBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<TelegramNotificationBackgroundTask> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                bool hasPendingPairing;

                await using (var scope = serviceScopeFactory.CreateAsyncScope())
                {
                    var service =
                        scope.ServiceProvider.GetRequiredService<TelegramNotificationService>();

                    await service.ProcessDeliveriesAsync(stoppingToken);

                    hasPendingPairing = service.HasPendingPairing;
                    if (hasPendingPairing)
                    {
                        await service.PollPairingAsync(stoppingToken);
                    }
                }

                if (!hasPendingPairing)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Telegram notification processing failed");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
}
