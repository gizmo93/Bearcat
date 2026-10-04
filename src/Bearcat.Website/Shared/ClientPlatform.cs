using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;

namespace Bearcat.Website.Shared;

public sealed class ClientPlatform : IDisposable
{
    private const string IsMacStateKey = "bearcat.clientPlatform.isMac";

    private readonly PersistentComponentState applicationState;
    private readonly PersistingComponentStateSubscription persistSubscription;

    public ClientPlatform(
        PersistentComponentState applicationState,
        IHttpContextAccessor httpContextAccessor
    )
    {
        this.applicationState = applicationState;
        persistSubscription = applicationState.RegisterOnPersisting(PersistIsMac);

        IsMac = applicationState.TryTakeFromJson<bool>(IsMacStateKey, out var persisted)
            ? persisted
            : IsMacUserAgent(httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString());
    }

    public bool IsMac { get; }

    private Task PersistIsMac()
    {
        applicationState.PersistAsJson(IsMacStateKey, IsMac);
        return Task.CompletedTask;
    }

    private static bool IsMacUserAgent(string? userAgent) =>
        userAgent?.Contains("Mac OS X", StringComparison.OrdinalIgnoreCase) == true;

    public void Dispose()
    {
        persistSubscription.Dispose();
    }
}
