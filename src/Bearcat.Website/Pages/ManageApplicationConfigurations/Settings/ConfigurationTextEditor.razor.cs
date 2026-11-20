using Bearcat.Domain.UseCases.ManageApplicationConfigurations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Bearcat.Website.Pages.ManageApplicationConfigurations.Settings;

public partial class ConfigurationTextEditor : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public ApplicationConfigurationPropertyDto Setting { get; set; } = null!;

    [Parameter]
    public EventCallback<ConfigurationSettingSaveRequest> OnSave { get; set; }

    private string savedText = "";
    private string editedText = "";
    private bool isEditing;

    private string DisplayedText => isEditing ? editedText : savedText;

    private bool IsDirty => isEditing && editedText != savedText;

    protected override void OnParametersSet()
    {
        var currentSavedText = (string)Setting.CurrentValue!;

        if (currentSavedText == savedText)
        {
            return;
        }

        savedText = currentSavedText;
        Discard();
    }

    private void OnTextChanged(string? text)
    {
        editedText = text ?? "";
        isEditing = true;
    }

    private void Discard()
    {
        editedText = "";
        isEditing = false;
    }

    private async Task OnKeyDownAsync(KeyboardEventArgs eventArgs)
    {
        if (eventArgs.Key == "Enter")
        {
            await SaveAsync();
        }
        else if (eventArgs.Key == "Escape")
        {
            Discard();
        }
    }

    private async Task SaveAsync()
    {
        if (!IsDirty)
        {
            return;
        }

        await OnSave.InvokeAsync(new ConfigurationSettingSaveRequest(Setting, editedText));
        Discard();
    }
}
