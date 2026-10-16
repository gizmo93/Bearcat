using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Domain.UseCases.ManageHosters;
using Bearcat.Domain.UseCases.ManageHosters.ReadModels;
using Bearcat.Domain.UseCases.ManageHosters.Repositories;
using Bearcat.Website.Pages.ManageHosters;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.FirstStartSetup;

public partial class FirstStartSetupHosterStep(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner
) : ComponentBase
{
    private const string PostInstallationDocumentationUrl =
        "https://gizmo93.github.io/Bearcat/post-installation/";

    [Parameter]
    [EditorRequired]
    public FirstStartSetupWizardState WizardState { get; set; } = null!;

    [Parameter]
    public EventCallback OnBack { get; set; }

    [Parameter]
    public EventCallback OnSkip { get; set; }

    [Parameter]
    public EventCallback OnNext { get; set; }

    private readonly HashSet<int> hosterRegistrationIdsWithRunningLogin = [];
    private IReadOnlyList<HosterRegistrationReadModel> hosterRegistrations = [];
    private bool isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadHosterRegistrationsAsync();
        isLoading = false;
    }

    private async Task LoadHosterRegistrationsAsync()
    {
        hosterRegistrations = await operationRunner.RunAsync(
            (IHosterConfigurationReadRepository repository) => repository.GetAllRegistrationsAsync()
        );
    }

    private TryLoginResult? GetLoginResult(int hosterRegistrationId)
    {
        return WizardState.LoginResultsByHosterRegistrationId.GetValueOrDefault(
            hosterRegistrationId
        );
    }

    private void SetHosterRegistrationSelected(int hosterRegistrationId, bool isSelected)
    {
        if (isSelected)
        {
            WizardState.SelectedHosterRegistrationIds.Add(hosterRegistrationId);
            return;
        }

        WizardState.SelectedHosterRegistrationIds.Remove(hosterRegistrationId);
    }

    private async Task TryLoginAsync(int hosterRegistrationId)
    {
        hosterRegistrationIdsWithRunningLogin.Add(hosterRegistrationId);
        StateHasChanged();

        try
        {
            var loginResult = await operationRunner.RunAsync(
                (HosterRegistrationService service) => service.TryLoginAsync(hosterRegistrationId)
            );

            WizardState.LoginResultsByHosterRegistrationId[hosterRegistrationId] = loginResult;
            await LoadHosterRegistrationsAsync();
        }
        finally
        {
            hosterRegistrationIdsWithRunningLogin.Remove(hosterRegistrationId);
        }
    }

    private async Task ShowAddHosterDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(AddOrEditHoster.FormModel)] = new HosterFormModel(),
        };

        var dialog = await dialogService.OpenAsync<AddOrEditHoster>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["AddHoster"],
                Description = L["HosterDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (dialog.Cancelled)
        {
            return;
        }

        var hosterRegistrationId = dialog.GetData<int>();

        await LoadHosterRegistrationsAsync();
        WizardState.SelectedHosterRegistrationIds.Add(hosterRegistrationId);

        await TryLoginAsync(hosterRegistrationId);
    }

    private async Task ShowCaptchaDialogAsync(HosterRegistrationReadModel hosterRegistration)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CaptchaVerificationDialog.HosterRegistrationId)] = hosterRegistration.Id,
            [nameof(CaptchaVerificationDialog.HosterRegistrationName)] = hosterRegistration.Name,
        };

        var dialog = await dialogService.OpenAsync<CaptchaVerificationDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["SolveCaptcha"],
                Description = hosterRegistration.Name,
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (dialog.Cancelled)
        {
            return;
        }

        toastService.Success(
            L["FirstStartSetupCaptchaVerificationCompleted", hosterRegistration.Name]
        );
        await LoadHosterRegistrationsAsync();
    }
}
