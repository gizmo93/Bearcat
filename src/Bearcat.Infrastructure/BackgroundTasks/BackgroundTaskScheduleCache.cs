using System.Collections.Concurrent;
using Bearcat.Abstractions.BackgroundTasks;
using Bearcat.Domain.UseCases.ManageBackgroundTasks.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Infrastructure.BackgroundTasks;

public class BackgroundTaskScheduleCache(IServiceScopeFactory serviceScopeFactory)
    : IBackgroundTaskScheduleCache
{
    private readonly ConcurrentDictionary<string, TimeSpan> overrides = [];
    private readonly ConcurrentDictionary<string, bool> enabledStates = [];
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private volatile bool isInitialized;

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (isInitialized)
        {
            return;
        }

        await initializationLock.WaitAsync(cancellationToken);

        try
        {
            if (isInitialized)
            {
                return;
            }

            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var readRepository =
                scope.ServiceProvider.GetRequiredService<IBackgroundTaskStateReadRepository>();
            var states = await readRepository.GetAllAsync(cancellationToken);

            foreach (var state in states)
            {
                enabledStates.TryAdd(state.Key, state.IsEnabled);

                if (state.IntervalOverride.HasValue)
                {
                    overrides.TryAdd(state.Key, state.IntervalOverride.Value);
                }
            }

            isInitialized = true;
        }
        finally
        {
            initializationLock.Release();
        }
    }

    public bool TryGetOverride(string key, out TimeSpan interval)
    {
        return overrides.TryGetValue(key, out interval);
    }

    public void SetOverride(string key, TimeSpan? interval)
    {
        if (interval.HasValue)
        {
            overrides[key] = interval.Value;
        }
        else
        {
            overrides.TryRemove(key, out _);
        }
    }

    public bool IsEnabled(string key)
    {
        return !enabledStates.TryGetValue(key, out var enabled) || enabled;
    }

    public void SetEnabled(string key, bool isEnabled)
    {
        enabledStates[key] = isEnabled;
    }
}
