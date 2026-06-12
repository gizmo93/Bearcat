using Bearcat.Abstractions.BackgroundTasks;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageBackgroundTasks.Repositories;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageBackgroundTasks;

public class BackgroundTaskStateService(
    IBackgroundTaskStateWriteRepository writeRepository,
    IBackgroundTaskScheduleCache scheduleCache,
    TimeProvider timeProvider
)
{
    private const int MaxErrorMessageLength = 2000;

    public static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(5);

    public async Task<bool> RegisterAsync(
        string key,
        string displayName,
        TimeSpan defaultInterval,
        CancellationToken cancellationToken = default
    )
    {
        var taskState = await GetOrCreateAsync(
            key: key,
            displayName: displayName,
            defaultInterval: defaultInterval,
            cancellationToken: cancellationToken
        );

        scheduleCache.SetEnabled(key, taskState.IsEnabled);
        scheduleCache.SetOverride(key, taskState.IntervalOverride);
        return taskState.IsEnabled;
    }

    public async Task MarkStartedAsync(
        string key,
        string displayName,
        CancellationToken cancellationToken = default
    )
    {
        var taskState = await GetOrCreateAsync(key, displayName, null, cancellationToken);
        taskState.LastStartedAt = timeProvider.GetLocalNow();
        taskState.LastFinishedAt = null;
        taskState.LastExecutionStatus = null;
        taskState.LastErrorMessage = null;
        taskState.UpdatedAt = taskState.LastStartedAt.Value;

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkSucceededAsync(
        string key,
        string displayName,
        CancellationToken cancellationToken = default
    )
    {
        var taskState = await GetOrCreateAsync(key, displayName, null, cancellationToken);
        taskState.LastFinishedAt = timeProvider.GetLocalNow();
        taskState.LastExecutionStatus = BackgroundTaskExecutionStatus.Success;
        taskState.LastErrorMessage = null;
        taskState.UpdatedAt = taskState.LastFinishedAt.Value;

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
        string key,
        string displayName,
        Exception exception,
        CancellationToken cancellationToken = default
    )
    {
        var taskState = await GetOrCreateAsync(key, displayName, null, cancellationToken);
        taskState.LastFinishedAt = timeProvider.GetLocalNow();
        taskState.LastExecutionStatus = BackgroundTaskExecutionStatus.Error;
        taskState.LastErrorMessage = TruncateErrorMessage(exception.Message);
        taskState.UpdatedAt = taskState.LastFinishedAt.Value;

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task SetIsEnabledAsync(
        int id,
        bool isEnabled,
        CancellationToken cancellationToken = default
    )
    {
        var taskState = await writeRepository.GetByIdAsync(id, cancellationToken);
        taskState.IsEnabled = isEnabled;
        taskState.UpdatedAt = timeProvider.GetLocalNow();

        await writeRepository.SaveChangesAsync(cancellationToken);

        scheduleCache.SetEnabled(taskState.Key, isEnabled);
    }

    public async Task SetIntervalOverrideAsync(
        int id,
        TimeSpan? interval,
        CancellationToken cancellationToken = default
    )
    {
        if (interval.HasValue && interval.Value < MinInterval)
        {
            throw new ArgumentOutOfRangeException(
                nameof(interval),
                interval,
                $"The interval override must be at least {MinInterval.TotalSeconds:0} seconds."
            );
        }

        var taskState = await writeRepository.GetByIdAsync(id, cancellationToken);
        taskState.IntervalOverride = interval;
        taskState.UpdatedAt = timeProvider.GetLocalNow();

        await writeRepository.SaveChangesAsync(cancellationToken);

        scheduleCache.SetOverride(taskState.Key, interval);
    }

    private async Task<BackgroundTaskState> GetOrCreateAsync(
        string key,
        string displayName,
        TimeSpan? defaultInterval,
        CancellationToken cancellationToken
    )
    {
        var now = timeProvider.GetLocalNow();
        var taskState = await writeRepository.GetByKeyOrDefaultAsync(key, cancellationToken);

        if (taskState is null)
        {
            taskState = new BackgroundTaskState
            {
                Key = key,
                DisplayName = displayName,
                IsEnabled = true,
                DefaultInterval = defaultInterval ?? TimeSpan.Zero,
                UpdatedAt = now,
            };
            writeRepository.Add(taskState);
            await writeRepository.SaveChangesAsync(cancellationToken);
            return taskState;
        }

        var hasChanges = false;

        if (taskState.DisplayName != displayName)
        {
            taskState.DisplayName = displayName;
            hasChanges = true;
        }

        if (defaultInterval.HasValue && taskState.DefaultInterval != defaultInterval.Value)
        {
            taskState.DefaultInterval = defaultInterval.Value;
            hasChanges = true;
        }

        if (hasChanges)
        {
            taskState.UpdatedAt = now;
            await writeRepository.SaveChangesAsync(cancellationToken);
        }

        return taskState;
    }

    private static string TruncateErrorMessage(string errorMessage)
    {
        return errorMessage.Length <= MaxErrorMessageLength
            ? errorMessage
            : errorMessage[..MaxErrorMessageLength];
    }
}
