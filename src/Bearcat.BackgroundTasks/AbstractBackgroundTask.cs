using Bearcat.Abstractions.BackgroundTasks;
using Bearcat.Domain.UseCases.ManageBackgroundTasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bearcat.BackgroundTasks;

public abstract class AbstractBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger logger
) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private bool registered;

    private string Key => GetType().FullName ?? GetType().Name;

    protected abstract string DisplayName { get; }

    protected abstract TimeSpan DefaultInterval { get; }

    protected abstract Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    );

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting {DisplayName} Background Task", DisplayName);
        await Task.Yield();

        var scheduleCache = ResolveScheduleCache();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessTickAsync(scheduleCache, stoppingToken);
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

            if (!await WaitForNextTickAsync(scheduleCache, stoppingToken))
            {
                break;
            }
        }
    }

    private IBackgroundTaskScheduleCache ResolveScheduleCache()
    {
        using var scope = serviceScopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IBackgroundTaskScheduleCache>();
    }

    private async Task ProcessTickAsync(
        IBackgroundTaskScheduleCache scheduleCache,
        CancellationToken stoppingToken
    )
    {
        if (registered && !scheduleCache.IsEnabled(Key))
        {
            return;
        }

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var stateService = scope.ServiceProvider.GetRequiredService<BackgroundTaskStateService>();

        if (!registered)
        {
            var isEnabled = await stateService.RegisterAsync(
                key: Key,
                displayName: DisplayName,
                defaultInterval: DefaultInterval,
                cancellationToken: stoppingToken
            );

            registered = true;

            if (!isEnabled)
            {
                return;
            }
        }

        var tickCancellationToken = scheduleCache.RegisterRunningTick(Key, stoppingToken);

        try
        {
            await stateService.MarkStartedAsync(Key, DisplayName, stoppingToken);

            await ExecuteTickAndMarkResultAsync(
                serviceProvider: scope.ServiceProvider,
                stateService: stateService,
                tickCancellationToken: tickCancellationToken,
                stoppingToken: stoppingToken
            );
        }
        finally
        {
            scheduleCache.UnregisterRunningTick(Key);
        }
    }

    private async Task ExecuteTickAndMarkResultAsync(
        IServiceProvider serviceProvider,
        BackgroundTaskStateService stateService,
        CancellationToken tickCancellationToken,
        CancellationToken stoppingToken
    )
    {
        Exception? tickException = null;

        try
        {
            await ExecuteTickAsync(serviceProvider, tickCancellationToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            tickException = exception;
        }

        if (tickCancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "{DisplayName} background task tick was canceled because the task was disabled",
                DisplayName
            );

            await MarkCanceledAsync(stoppingToken);
        }
        else if (tickException is not null)
        {
            logger.LogError(
                tickException,
                "{DisplayName} background task tick failed",
                DisplayName
            );

            await stateService.MarkFailedAsync(Key, DisplayName, tickException, stoppingToken);
        }
        else
        {
            await stateService.MarkSucceededAsync(Key, DisplayName, stoppingToken);
        }
    }

    private async Task MarkCanceledAsync(CancellationToken stoppingToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var stateService = scope.ServiceProvider.GetRequiredService<BackgroundTaskStateService>();
        await stateService.MarkCanceledAsync(Key, DisplayName, stoppingToken);
    }

    private async Task<bool> WaitForNextTickAsync(
        IBackgroundTaskScheduleCache scheduleCache,
        CancellationToken stoppingToken
    )
    {
        await scheduleCache.EnsureInitializedAsync(stoppingToken);

        var elapsed = TimeSpan.Zero;

        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = scheduleCache.TryGetOverride(Key, out var overrideInterval)
                ? overrideInterval
                : DefaultInterval;

            if (elapsed >= interval)
            {
                return true;
            }

            var remaining = interval - elapsed;
            var delay = remaining < PollInterval ? remaining : PollInterval;

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return false;
            }

            elapsed += delay;
        }

        return false;
    }
}
