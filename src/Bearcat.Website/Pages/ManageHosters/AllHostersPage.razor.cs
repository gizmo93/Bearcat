using Bearcat.Domain.UseCases.ManageHosters;
using Bearcat.Domain.UseCases.ManageHosters.ReadModels;
using Bearcat.Domain.UseCases.ManageHosters.Repositories;
using BlazorBlueprint.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageHosters;

public partial class AllHostersPage(
    IHosterConfigurationReadRepository readRepository,
    DialogService dialogService,
    ToastService toastService
)
{
    private IReadOnlyList<HosterRegistrationReadModel> hosters = [];
    private HosterRegistrationService hosterRegistrationService = null!;

    protected override async Task OnInitializedAsync()
    {
        await LoadHostersAsync();
        hosterRegistrationService = ScopedServices.GetRequiredService<HosterRegistrationService>();
    }

    private async Task LoadHostersAsync()
    {
        hosters = await readRepository.GetAllRegistrationsAsync();
    }

    private async Task ShowAddDialogAsync()
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

        if (!dialog.Cancelled)
        {
            await LoadHostersAsync();
        }
    }

    private async Task ShowEditDialogAsync(HosterRegistrationReadModel hosterRegistration)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(AddOrEditHoster.FormModel)] = new HosterFormModel
            {
                Name = hosterRegistration.Name,
                FullClassName = hosterRegistration.FullClassName,
                IsEdit = true,
                HosterRegistrationId = hosterRegistration.Id,
            },
        };

        var dialog = await dialogService.OpenAsync<AddOrEditHoster>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditNamedItem", hosterRegistration.Name],
                Description = L["HosterDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadHostersAsync();
        }
    }

    private async Task ToggleIsActiveAsync(HosterRegistrationReadModel hoster)
    {
        await hosterRegistrationService.ToggleIsActiveAsync(hoster.Id);

        toastService.Success(
            hoster.IsActive
                ? L["HosterRegistrationDeactivated", hoster.Name]
                : L["HosterRegistrationActivated", hoster.Name]
        );
        await LoadHostersAsync();
    }

    private async Task DeleteAsync(HosterRegistrationReadModel hoster)
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", hoster.Name],
            L["DeleteHosterRegistrationConfirmation", hoster.Name],
            new ConfirmDialogOptions
            {
                ConfirmText = L["Delete"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        if (!result.Confirmed)
        {
            return;
        }

        await hosterRegistrationService.RemoveAsync(hoster.Id);
        await LoadHostersAsync();
    }

    private async Task TryLoginAsync(HosterRegistrationReadModel hoster)
    {
        await using var scope = ScopedServices.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<HosterRegistrationService>();

        var result = await service.TryLoginAsync(hoster.Id);

        if (result.IsSuccess)
        {
            toastService.Success(L["LoginSuccessful", hoster.Name]);
            await LoadHostersAsync();
            return;
        }

        toastService.Error(L["LoginFailed", hoster.Name, result.ErrorMessage ?? string.Empty]);
        await LoadHostersAsync();
    }

    private async Task ShowCaptchaDialogAsync(HosterRegistrationReadModel hoster)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CaptchaVerificationDialog.HosterRegistrationId)] = hoster.Id,
            [nameof(CaptchaVerificationDialog.HosterRegistrationName)] = hoster.Name,
        };

        var dialog = await dialogService.OpenAsync<CaptchaVerificationDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["SolveCaptcha"],
                Description = hoster.Name,
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            toastService.Success($"Captcha verification for {hoster.Name} completed.");
            await LoadHostersAsync();
        }
    }
}
