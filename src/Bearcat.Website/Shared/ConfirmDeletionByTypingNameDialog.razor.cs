using System.Globalization;
using System.Text.Encodings.Web;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Shared;

public partial class ConfirmDeletionByTypingNameDialog : ComponentBase
{
    private const string NameInputId = "confirm-deletion-name";

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public string NameToConfirm { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public string AffectedItemsLabel { get; set; } = null!;

    [Parameter]
    public int AffectedItemsCount { get; set; }

    private string? typedName;

    private bool IsTypedNameMatching =>
        string.Equals(typedName, NameToConfirm, StringComparison.Ordinal);

    private MarkupString ConfirmationLabel =>
        new(
            string.Format(
                CultureInfo.CurrentCulture,
                L["TypeNameToConfirmDeletion"].Value,
                $"<strong class=\"font-semibold break-all\">{HtmlEncoder.Default.Encode(NameToConfirm)}</strong>"
            )
        );

    private async Task ConfirmAsync()
    {
        await DialogRef.CloseAsync(DialogResult.Ok());
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
