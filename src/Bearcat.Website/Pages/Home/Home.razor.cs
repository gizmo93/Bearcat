using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.UseCases.FirstStartSetup.Repositories;
using Bearcat.Domain.UseCases.FirstStartSetup.Status;
using Bearcat.Domain.UseCases.ManageApplicationConfigurations;
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
    private bool showsAssistant;
    private PersistingComponentStateSubscription persistSubscription;

    private bool ShowsOptionalSetupChecklist()
    {
        var isEveryOptionalStepDone =
            setupStatus is
            {
                HasNfoDatabaseOrMediaDatabaseRegistration: true,
                HasImageHosterRegistration: true,
                HasLinkCrypterRegistration: true,
                HasConnectedTelegramConfiguration: true,
                HasDistributionSiteRegistration: true,
                HasProxyServer: true,
            };

        return showsAssistant
            && setupStatus is { IsRequiredSetupComplete: true }
            && !isEveryOptionalStepDone;
    }

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
        showsAssistant = operationRunner.Run(
            (IApplicationConfigurationProvider configuration) =>
                configuration.GetValue<FirstStartSetupConfiguration>(c =>
                    c.ShowAssistantOnActivityPage
                )
        );
    }

    private async Task HideAssistantAsync()
    {
        showsAssistant = false;
        await operationRunner.RunAsync(
            (ApplicationConfigurationService service) =>
                service.SaveOverrideAsync(
                    configurationKey: "FirstStartSetup",
                    propertyName: nameof(FirstStartSetupConfiguration.ShowAssistantOnActivityPage),
                    value: false,
                    cancellationToken: CancellationToken.None
                )
        );
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

    public void Dispose()
    {
        persistSubscription.Dispose();
    }
}
