using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageBackgroundTasks.Repositories;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageBackgroundTasks;

public class BackgroundTaskStateService(
    IBackgroundTaskStateWriteRepository writeRepository,
    TimeProvider timeProvider
)
{
    private const int MaxErrorMessageLength = 2000;

    public async Task<bool> IsEnabledAsync(
        string key,
        string displayName,
        CancellationToken cancellationToken = default
    )
    {
        var taskState = await GetOrCreateAsync(key, displayName, cancellationToken);
        return taskState.IsEnabled;
    }

    public async Task MarkStartedAsync(
        string key,
        string displayName,
        CancellationToken cancellationToken = default
    )
    {
        var taskState = await GetOrCreateAsync(key, displayName, cancellationToken);
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
        var taskState = await GetOrCreateAsync(key, displayName, cancellationToken);
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
        var taskState = await GetOrCreateAsync(key, displayName, cancellationToken);
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
    }

    private async Task<BackgroundTaskState> GetOrCreateAsync(
        string key,
        string displayName,
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
                UpdatedAt = now,
            };
            writeRepository.Add(taskState);
            await writeRepository.SaveChangesAsync(cancellationToken);
            return taskState;
        }

        if (taskState.DisplayName != displayName)
        {
            taskState.DisplayName = displayName;
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
