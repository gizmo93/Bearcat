using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Shared;

public partial class FolderPathInput(DialogService dialogService) : ComponentBase
{
    [Parameter]
    public string? Value { get; set; }

    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    private static IReadOnlyList<string> FileSystemRootPaths =>
        OperatingSystem.IsWindows() ? Directory.GetLogicalDrives() : ["/"];

    private async Task OpenFolderSelectionDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(FolderSelectionDialog.BaseFolderPaths)] = FileSystemRootPaths,
            [nameof(FolderSelectionDialog.SelectedFolderPath)] = Value,
        };

        var result = await dialogService.OpenAsync<FolderSelectionDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["SelectFolder"],
                Description = L["SelectFolderDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
            }
        );

        if (result.Cancelled)
        {
            return;
        }

        var selectedFolderPath = result.GetData<string>();

        if (!string.IsNullOrWhiteSpace(selectedFolderPath))
        {
            await ValueChanged.InvokeAsync(selectedFolderPath);
        }
    }
}
