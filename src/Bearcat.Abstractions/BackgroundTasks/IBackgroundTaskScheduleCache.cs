namespace Bearcat.Abstractions.BackgroundTasks;

public interface IBackgroundTaskScheduleCache
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken);

    bool TryGetOverride(string key, out TimeSpan interval);

    void SetOverride(string key, TimeSpan? interval);

    bool IsEnabled(string key);

    void SetEnabled(string key, bool isEnabled);

    CancellationToken RegisterRunningTick(string key, CancellationToken stoppingToken);

    void UnregisterRunningTick(string key);
}
