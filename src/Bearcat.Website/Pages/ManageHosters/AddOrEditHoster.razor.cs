using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Domain.UseCases.ManageHosters;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Bearcat.Website.Pages.ManageHosters;

public partial class AddOrEditHoster(
    IHosterFactory hosterFactory,
    HosterRegistrationService hosterRegistrationService
) : ComponentBase
{
    [Parameter]
    public HosterFormModel FormModel { get; set; } = new();

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private IReadOnlyList<HosterDto> hosterReadModels = [];
    private HosterDto? selectedHoster;
    private EditContext editContext = null!;
    private ValidationMessageStore? messageStore;
    private readonly HashSet<string> displayedPasswords = [];

    protected override void OnInitialized()
    {
        hosterReadModels = hosterFactory.GetHosterReadModels();
        editContext = new EditContext(FormModel);
        editContext.OnValidationRequested += HandleValidationRequested;
        messageStore = new ValidationMessageStore(editContext);

        if (FormModel.IsEdit)
        {
            selectedHoster = hosterReadModels.First(h =>
                h.HosterClassName == FormModel.FullClassName
            );
        }
    }

    private async Task SaveAsync()
    {
        if (!FormModel.IsEdit)
        {
            await hosterRegistrationService.RegisterHosterAsync(
                name: FormModel.Name,
                isActive: true,
                configuration: FormModel.Configuration,
                hosterClassName: FormModel.FullClassName,
                maxParallelUploadsOverride: FormModel.MaxParallelUploadsOverride
            );
        }
        else
        {
            await hosterRegistrationService.UpdateRegistrationAsync(
                FormModel.HosterRegistrationId!.Value,
                FormModel.Name,
                FormModel.Configuration,
                FormModel.MaxParallelUploadsOverride
            );
        }

        await DialogRef.CloseAsync(DialogResult.Ok());
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs args)
    {
        messageStore!.Clear();

        if (string.IsNullOrWhiteSpace(FormModel.Name))
        {
            messageStore.Add(() => FormModel.Name, L["NameIsRequired"]);
        }

        if (selectedHoster is null)
        {
            messageStore.Add(() => FormModel.FullClassName, L["SelectHosterRequired"]);
        }

        if (selectedHoster is null)
        {
            return;
        }

        if (FormModel.IsEdit)
        {
            return;
        }

        var missingConfigs = selectedHoster
            .ConfigurationKeys.Where(key =>
                string.IsNullOrWhiteSpace(FormModel.Configuration.GetValueOrDefault(key))
            )
            .ToList();

        foreach (var config in missingConfigs)
        {
            messageStore.Add(
                () => FormModel.Configuration,
                L["ConfigurationValueRequired", config]
            );
        }
    }

    private void OnSelectedHosterChanged()
    {
        selectedHoster = string.IsNullOrEmpty(FormModel.FullClassName)
            ? null
            : hosterReadModels.First(h => h.HosterClassName == FormModel.FullClassName);

        displayedPasswords.Clear();

        if (!FormModel.IsEdit)
        {
            FormModel.Configuration = new Dictionary<string, string>();
        }
    }

    private void OnConfigurationValueChanged(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            FormModel.Configuration.Remove(key);
            return;
        }

        FormModel.Configuration[key] = value;
    }

    private void ToggleShowHidePassword(string key)
    {
        if (displayedPasswords.Add(key))
        {
            return;
        }

        displayedPasswords.Remove(key);
    }
}
