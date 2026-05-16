using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Domain.UseCases.ManageHosters;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;

namespace Bearcat.Website.Pages.ManageHosters;

public partial class AddOrEditHoster(
    IHosterFactory hosterFactory,
    HosterRegistrationService hosterRegistrationService
) : ComponentBase
{
    [Parameter]
    public HosterFormModel FormModel { get; set; } = new();

    [CascadingParameter]
    public IMudDialogInstance MudDialog { get; set; } = null!;

    private IReadOnlyList<HosterDto> hosterReadModels = [];

    private HosterDto? selectedHoster;

    private EditContext editContext = null!;

    private ValidationMessageStore? messageStore;

    private HashSet<string> displayedPasswords = [];

    protected override void OnInitialized()
    {
        hosterReadModels = hosterFactory.GetHosterReadModels();
        editContext = new EditContext(FormModel);
        editContext.OnValidationRequested += HandleValidationRequested;
        messageStore = new(editContext);

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
                hosterClassName: FormModel.FullClassName
            );
        }
        else
        {
            await hosterRegistrationService.UpdateRegistrationAsync(
                FormModel.HosterRegistrationId!.Value,
                FormModel.Name,
                FormModel.Configuration
            );
        }

        MudDialog.Close();
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs args)
    {
        messageStore!.Clear();

        if (string.IsNullOrWhiteSpace(FormModel.Name))
        {
            messageStore.Add(() => FormModel.Name, "Name is required");
        }

        if (selectedHoster is null)
        {
            messageStore.Add(() => FormModel.FullClassName, "You must select a hoster");
        }

        if (selectedHoster is not null)
        {
            var missingConfigs = selectedHoster
                .ConfigurationKeys.Except(FormModel.Configuration.Keys)
                .ToList();

            foreach (var config in missingConfigs)
            {
                messageStore.Add(
                    () => FormModel.Configuration,
                    $"Configuration '{config}' is required"
                );
            }
        }
    }

    private void OnSelectedHosterChanged()
    {
        selectedHoster = string.IsNullOrEmpty(FormModel.FullClassName)
            ? null
            : hosterReadModels.First(h => h.HosterClassName == FormModel.FullClassName);

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

        FormModel.Configuration[key] = value!;
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
