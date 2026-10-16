using Bearcat.Domain.UseCases.FirstStartSetup.Repositories;
using Bearcat.Domain.UseCases.FirstStartSetup.Status;
using Bearcat.Website.Pages.FirstStartSetup;
using Bearcat.Website.ScopedOperations;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home;

public sealed partial class Home(
    IScopedOperationRunner operationRunner,
    NavigationManager navigationManager,
    PersistentComponentState applicationState,
    FirstStartSetupBrowserState browserState
) : IDisposable
{
    private const string SetupStatusKey = "bearcat.home.firstStartSetupStatus";

    private FirstStartSetupDatabaseStatus? setupStatus;
    private PersistingComponentStateSubscription persistSubscription;

    private bool ShowsOptionalSetupChecklist =>
        setupStatus is { IsRequiredSetupComplete: true }
        && !setupStatus.OptionalSetupStatus.IsEverythingSetUp
        && !browserState.AreOptionalStepsHidden;

    protected override async Task OnInitializedAsync()
    {
        persistSubscription = applicationState.RegisterOnPersisting(PersistSetupStatus);

        var loadedSetupStatus = applicationState.TryTakeFromJson<FirstStartSetupDatabaseStatus>(
            SetupStatusKey,
            out var persisted
        )
            ? persisted!
            : await LoadSetupStatusAsync();

        if (loadedSetupStatus.IsFreshInstallation && !browserState.IsSetupAutoOpenDisabled)
        {
            navigationManager.NavigateTo("/setup", replace: true);
            return;
        }

        setupStatus = loadedSetupStatus;
    }

    private Task<FirstStartSetupDatabaseStatus> LoadSetupStatusAsync()
    {
        return operationRunner.RunAsync(
            (IFirstStartSetupStatusRepository repository) => repository.GetDatabaseStatusAsync()
        );
    }

    private Task PersistSetupStatus()
    {
        if (setupStatus is not null)
        {
            applicationState.PersistAsJson(SetupStatusKey, setupStatus);
        }

        return Task.CompletedTask;
    }

    private async Task HideOptionalSetupChecklistAsync()
    {
        await browserState.HideOptionalStepsAsync();
    }

    public void Dispose()
    {
        persistSubscription.Dispose();
    }
}
