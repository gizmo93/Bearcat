using System.Globalization;
using Bearcat.Domain.UseCases.ManageApplicationConfigurations;
using Bearcat.Website.Formatting;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Bearcat.Website.Pages.ManageApplicationConfigurations.Settings;

public partial class ConfigurationNumberEditor(ToastService toastService) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public ApplicationConfigurationPropertyDto Setting { get; set; } = null!;

    [Parameter]
    public EventCallback<ConfigurationSettingSaveRequest> OnSave { get; set; }

    private string? savedText;
    private string? editedText;
    private bool isEditing;

    private string? DisplayedText => isEditing ? editedText : savedText;

    private bool IsDirty => isEditing && editedText != savedText;

    private bool IsNullable => Nullable.GetUnderlyingType(Setting.ValueType) is not null;

    private Type NumberType => Nullable.GetUnderlyingType(Setting.ValueType) ?? Setting.ValueType;

    private string InputMode => NumberType == typeof(decimal) ? "decimal" : "numeric";

    private string? Placeholder => IsNullable ? L["NoLimit"].Value : null;

    protected override void OnParametersSet()
    {
        var currentSavedText = ConfigurationSettingFormatter.FormatEditorText(Setting.CurrentValue);

        if (currentSavedText == savedText)
        {
            return;
        }

        savedText = currentSavedText;
        Discard();
    }

    private void OnTextChanged(string? text)
    {
        editedText = text;
        isEditing = true;
    }

    private void Discard()
    {
        editedText = null;
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

        if (!TryParseEditedText(out var value))
        {
            toastService.Error(L["ConfigurationValueInvalid", L[Setting.DisplayName]]);
            return;
        }

        await OnSave.InvokeAsync(new ConfigurationSettingSaveRequest(Setting, value));
        Discard();
    }

    private bool TryParseEditedText(out object? value)
    {
        value = null;

        if (string.IsNullOrWhiteSpace(editedText))
        {
            return IsNullable;
        }

        if (NumberType == typeof(int))
        {
            var isValid = int.TryParse(
                editedText,
                NumberStyles.Integer,
                CultureInfo.CurrentCulture,
                out var intValue
            );
            value = intValue;
            return isValid;
        }

        if (NumberType == typeof(decimal))
        {
            var isValid = DecimalInputConverter.TryParse(editedText, out var decimalValue);
            value = decimalValue;
            return isValid;
        }

        throw new NotSupportedException(
            $"Configuration value type {Setting.ValueType.Name} has no number editor."
        );
    }
}
