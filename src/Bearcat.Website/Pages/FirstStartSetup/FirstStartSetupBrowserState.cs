using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.JSInterop;

namespace Bearcat.Website.Pages.FirstStartSetup;

public sealed class FirstStartSetupBrowserState : IDisposable
{
    private const string SetupAutoOpenDisabledKey = "bearcat.firstStartSetup.autoOpenDisabled";
    private const string OptionalStepsHiddenKey = "bearcat.firstStartSetup.optionalStepsHidden";

    private readonly PersistentComponentState applicationState;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly IJSRuntime js;
    private readonly PersistingComponentStateSubscription persistSubscription;

    public FirstStartSetupBrowserState(
        PersistentComponentState applicationState,
        IHttpContextAccessor httpContextAccessor,
        IJSRuntime js
    )
    {
        this.applicationState = applicationState;
        this.httpContextAccessor = httpContextAccessor;
        this.js = js;
        persistSubscription = applicationState.RegisterOnPersisting(PersistValues);

        IsSetupAutoOpenDisabled = RestoreValue(SetupAutoOpenDisabledKey);
        AreOptionalStepsHidden = RestoreValue(OptionalStepsHiddenKey);
    }

    public bool IsSetupAutoOpenDisabled { get; private set; }

    public bool AreOptionalStepsHidden { get; private set; }

    public async Task DisableSetupAutoOpenAsync()
    {
        IsSetupAutoOpenDisabled = true;
        await WriteCookieAsync(SetupAutoOpenDisabledKey);
    }

    public async Task HideOptionalStepsAsync()
    {
        AreOptionalStepsHidden = true;
        await WriteCookieAsync(OptionalStepsHiddenKey);
    }

    private bool RestoreValue(string key)
    {
        if (applicationState.TryTakeFromJson<bool>(key, out var persisted))
        {
            return persisted;
        }

        return httpContextAccessor.HttpContext?.Request.Cookies.TryGetValue(key, out var cookie)
                == true
            && cookie == "true";
    }

    private Task PersistValues()
    {
        applicationState.PersistAsJson(SetupAutoOpenDisabledKey, IsSetupAutoOpenDisabled);
        applicationState.PersistAsJson(OptionalStepsHiddenKey, AreOptionalStepsHidden);
        return Task.CompletedTask;
    }

    private async Task WriteCookieAsync(string key)
    {
        try
        {
            await js.InvokeVoidAsync("bearcat.setCookie", key, "true");
        }
        catch (JSException) { }
    }

    public void Dispose()
    {
        persistSubscription.Dispose();
    }
}
