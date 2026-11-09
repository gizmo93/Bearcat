using System.Collections.Concurrent;
using Bearcat.Abstractions.BackgroundTasks;
using Bearcat.Domain.UseCases.ManageBackgroundTasks.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Infrastructure.BackgroundTasks;

public sealed class BackgroundTaskScheduleCache(IServiceScopeFactory serviceScopeFactory)
    : IBackgroundTaskScheduleCache,
        IDisposable
{
    private readonly ConcurrentDictionary<string, TimeSpan> overrides = [];
    private readonly ConcurrentDictionary<string, bool> enabledStates = [];
    private readonly Dictionary<string, CancellationTokenSource> runningTickTokenSources = new();
    private readonly Lock runningTickTokenSourcesLock = new();
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
        lock (runningTickTokenSourcesLock)
        {
            enabledStates[key] = isEnabled;

            if (!isEnabled && runningTickTokenSources.TryGetValue(key, out var tokenSource))
            {
                tokenSource.Cancel();
            }
        }
    }

    public CancellationToken RegisterRunningTick(string key, CancellationToken stoppingToken)
    {
        var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        lock (runningTickTokenSourcesLock)
        {
            runningTickTokenSources[key] = tokenSource;

            if (!IsEnabled(key))
            {
                tokenSource.Cancel();
            }
        }

        return tokenSource.Token;
    }

    public void UnregisterRunningTick(string key)
    {
        lock (runningTickTokenSourcesLock)
        {
            if (runningTickTokenSources.Remove(key, out var tokenSource))
            {
                tokenSource.Dispose();
            }
        }
    }

    public void Dispose()
    {
        initializationLock.Dispose();
    }
}
