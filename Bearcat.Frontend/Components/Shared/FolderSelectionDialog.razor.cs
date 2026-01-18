using BearCat.Core.Domain.Abstractions;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components.Icons.Regular;

namespace Bearcat.Frontend.Components.Shared;

public partial class FolderSelectionDialog(
    IFileSystemService fileSystemService)
    : ComponentBase, IDialogContentComponent<string>
{
    [Parameter] 
    public string Content { get; set; } = null!;
    
    [CascadingParameter] 
    public FluentDialog Dialog { get; set; } = null!;

    private ITreeViewItem? selectedFolderPath;

    private List<TreeViewItem> directoryTree = new();

    private readonly Icon iconCollapsed = new Size20.Folder();
    private readonly Icon iconExpanded = new Size20.FolderOpen();

    protected override void OnInitialized()
    {
        directoryTree = [GetDirectoryTree()];
    }

    private async Task OnSaveAsync()
    {
        try
        {
            await Dialog.CloseAsync(DialogResult.Ok(selectedFolderPath!.Id));
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private TreeViewItem GetDirectoryTree()
    {
        var rootPath = Content;
        var item = CreateTreeViewItem(rootPath);
        return item;
    }

    private Task OnExpandedAsync(TreeViewItemExpandedEventArgs e)
    {
        e.CurrentItem.Items = e.Expanded
            ? GetTreeViewItems(e.CurrentItem.Id)
            : TreeViewItem.LoadingTreeViewItems;

        return Task.CompletedTask;
    }

    private List<TreeViewItem> GetTreeViewItems(string path)
    {
        return fileSystemService.GetFoldersInPath(path)
            .Select(CreateTreeViewItem)
            .ToList();
    }
    
    private TreeViewItem CreateTreeViewItem(string path)
    {
        return new TreeViewItem
        {
            Id = path,
            Text = Path.GetFileName(path),
            Items = TreeViewItem.LoadingTreeViewItems,
            IconCollapsed = iconCollapsed,
            IconExpanded = iconExpanded,
            OnExpandedAsync = OnExpandedAsync
        };
    }
}
