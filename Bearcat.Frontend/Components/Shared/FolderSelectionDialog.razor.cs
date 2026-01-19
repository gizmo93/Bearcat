using BearCat.Core.Domain.Abstractions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Bearcat.Frontend.Components.Shared;

public partial class FolderSelectionDialog(
    IFileSystemService fileSystemService)
    : ComponentBase
{
    [Parameter]
    public string BaseFolderPath { get; set; } = null!;
    
    [CascadingParameter]
    public IMudDialogInstance MudDialog { get; set; } = null!;
    
    private List<TreeItemData<string?>> initialTreeItems = [];

    private string? selectedItem;

    protected override void OnInitialized()
    {
        InitializeTreeData();
    }

    private void Save()
    {
        var result = !string.IsNullOrEmpty(selectedItem)
            ? DialogResult.Ok(selectedItem)
            : DialogResult.Cancel();
        
        MudDialog.Close(result);
    }

    private void InitializeTreeData()
    {
        var item = CreateTreeViewItem(BaseFolderPath);
        initialTreeItems = [item];
    }

    private Task<IReadOnlyCollection<TreeItemData<string?>>> GetTreeViewItems(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult<IReadOnlyCollection<TreeItemData<string?>>>(new List<TreeItemData<string?>>());
        }
        
        var items = fileSystemService.GetFoldersInPath(path)
            .Select(CreateTreeViewItem)
            .ToList();

        return Task.FromResult<IReadOnlyCollection<TreeItemData<string?>>>(items);
    }
    
    private static TreeItemData<string?> CreateTreeViewItem(string path)
    {
        return new TreeItemData<string?>
        {
            Text = string.IsNullOrWhiteSpace(Path.GetFileName(path))
                ? path
                : Path.GetFileName(path),
            Icon = Icons.Material.Filled.Folder,
            Value = path,
            Expanded = false,
            Expandable = true,
            Selected = false,
            Visible = true,
        };
    }
}
