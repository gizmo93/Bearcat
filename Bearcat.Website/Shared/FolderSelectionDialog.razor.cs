using BearCat.Core.Domain.Abstractions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Bearcat.Website.Shared;

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
    
    private string? searchPhrase;
    
    private MudTreeView<string> treeView = null!;

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
    
    private async Task OnTextChangedAsync(string? search) 
    {
        searchPhrase = search;
        await treeView.FilterAsync();
    }
    
    private Task<bool> MatchesName(ITreeItemData<string?> item)
    {
        if (string.IsNullOrEmpty(searchPhrase))
        {
            return Task.FromResult(true);
        }
        
        if (string.IsNullOrEmpty(item.Text))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(item.Text.Contains(searchPhrase, StringComparison.OrdinalIgnoreCase));
    }
    
    private static void OnItemsLoaded(ITreeItemData<string?> treeItemData, IReadOnlyCollection<ITreeItemData<string?>> children)
    {
        treeItemData.Children = children.ToList();
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
