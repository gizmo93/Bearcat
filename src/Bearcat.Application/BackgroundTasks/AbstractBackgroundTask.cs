using Bearcat.Domain.UseCases.ManageBackgroundTasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public abstract class AbstractBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger logger
) : BackgroundService
{
    private string Key => GetType().FullName ?? GetType().Name;

    protected abstract string DisplayName { get; }

    protected abstract TimeSpan Interval { get; }

    protected abstract Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    );

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting {DisplayName} Background Task", DisplayName);
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "{DisplayName} background task orchestration failed",
                    DisplayName
                );
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessTickAsync(CancellationToken stoppingToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var stateService = scope.ServiceProvider.GetRequiredService<BackgroundTaskStateService>();

        if (!await stateService.IsEnabledAsync(Key, DisplayName, stoppingToken))
        {
            return;
        }

        await stateService.MarkStartedAsync(Key, DisplayName, stoppingToken);

        try
        {
            await ExecuteTickAsync(scope.ServiceProvider, stoppingToken);
            await stateService.MarkSucceededAsync(Key, DisplayName, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "{DisplayName} background task tick failed", DisplayName);
            await stateService.MarkFailedAsync(Key, DisplayName, exception, stoppingToken);
        }
    }
}
