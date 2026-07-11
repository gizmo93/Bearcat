using Bearcat.Abstractions.Notifications;
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
                    var processor =
                        scope.ServiceProvider.GetRequiredService<ITelegramNotificationProcessor>();

                    await processor.ProcessDeliveriesAsync(stoppingToken);

                    hasPendingPairing = processor.HasPendingPairing;
                    if (hasPendingPairing)
                    {
                        await processor.PollPairingAsync(stoppingToken);
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
