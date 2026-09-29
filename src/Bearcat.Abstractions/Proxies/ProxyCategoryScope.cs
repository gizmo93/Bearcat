namespace Bearcat.Abstractions.Proxies;

public static class ProxyCategoryScope
{
    private static readonly AsyncLocal<ProxyCategoryScopeState?> CurrentState = new();

    public static ProxyCategoryScopeState? Current => CurrentState.Value;

    public static IDisposable Enter(ProxyCategory category)
    {
        return Enter(category, ProxySelection.UseCategoryDefault, proxyServerId: null);
    }

    public static IDisposable Enter(
        ProxyCategory category,
        ProxySelection proxySelection,
        int? proxyServerId
    )
    {
        var previousState = CurrentState.Value;
        CurrentState.Value = new ProxyCategoryScopeState(category, proxySelection, proxyServerId);

        return new ProxyCategoryScopeExit(previousState);
    }

    private sealed class ProxyCategoryScopeExit(ProxyCategoryScopeState? previousState)
        : IDisposable
    {
        public void Dispose()
        {
            CurrentState.Value = previousState;
        }
    }
}
