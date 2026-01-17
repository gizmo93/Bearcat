using BearCat.Core.Domain.Abstractions.Hoster;
using BearCat.Core.Domain.UseCases.ManageHosters;
using BearCat.Core.Domain.UseCases.ManageUploads;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Bearcat.Frontend.Components.Pages.ManageHosters;

public partial class AddOrEditHoster(
    IHosterFactory hosterFactory,
    HosterRegistrationService hosterRegistrationService)
    : ComponentBase, IDialogContentComponent<HosterFormModel>
{
    [Parameter] 
    public HosterFormModel Content { get; set; } = new();

    [CascadingParameter]
    public FluentDialog? Dialog { get; set; }
    
    private IReadOnlyList<HosterReadModel> hosterReadModels = [];

    private HosterReadModel? selectedHoster;
    
    private EditContext editContext = null!;
    
    private ValidationMessageStore? messageStore;
    
    protected override void OnInitialized()
    {
        hosterReadModels = hosterFactory.GetHosterReadModels();
        editContext = new EditContext(Content);
        editContext.OnValidationRequested += HandleValidationRequested;
        messageStore = new(editContext);
    }
    
    private async Task SaveAsync()
    {
        if (!editContext.Validate())
        {
            return;
        }

        if (!Content.IsEdit)
        {
            await hosterRegistrationService.RegisterHosterAsync(
                name: Content.Name,
                isActive: true,
                configuration: Content.Configuration,
                hosterClassName: Content.FullClassName);   
        }
        else
        {
            await hosterRegistrationService.UpdateRegistrationAsync(
                Content.HosterRegistrationId!.Value,
                Content.Name,
                Content.Configuration);
        }
        
        await Dialog!.CloseAsync(Content);
    }

    private async Task CancelAsync()
    {
        await Dialog!.CancelAsync();
    }
    
    private void HandleValidationRequested(object? sender,
        ValidationRequestedEventArgs args)
    {
        messageStore!.Clear();

        if (string.IsNullOrWhiteSpace(Content.Name))
        {
            messageStore.Add(() => Content.Name, "Name is required");
        }
        
        if (selectedHoster is null)
        {
            messageStore.Add(() => Content.FullClassName, "You must select a hoster");
        }

        if (selectedHoster is not null)
        {
            var missingConfigs = selectedHoster.ConfigurationKeys
                .Except(Content.Configuration.Keys)
                .ToList();

            foreach (var config in missingConfigs)
            {
                messageStore.Add(() => Content.Configuration, $"Configuration '{config}' is required");
            }
        }
    }

    private void OnSelectedHosterChanged(HosterReadModel? hoster)
    {
        selectedHoster = hoster;

        if (!Content.IsEdit)
        {
            Content.Configuration = new Dictionary<string, string>();
        }
    }
    
    private void OnConfigurationValueChanged(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Content.Configuration.Remove(key);
            return;
        }
        
        Content.Configuration[key] = value!;
    }
}

