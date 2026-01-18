using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components.Icons.Regular;

namespace Bearcat.Frontend.Components.Pages.ManageReleases;

public partial class FolderSelectionDialog(IConfiguration configuration) : ComponentBase, IDialogContentComponent
{
    [CascadingParameter] public FluentDialog Dialog { get; set; } = null!;

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
        var rootPath = configuration.GetRequiredSection("ReleaseDataDirectory").Value!;

        return GetTreeViewItem(rootPath);
    }

    private TreeViewItem GetTreeViewItem(string path)
    {
        var treeViewItem = new TreeViewItem
        {
            Id = path,
            Text = Path.GetFileName(path),
            Items = new List<TreeViewItem>(),
            IconCollapsed = iconCollapsed,
            IconExpanded = iconExpanded,
        };

        var children = new List<TreeViewItem>();

        foreach (var folder in Directory.GetDirectories(path, "*",
                     enumerationOptions: new EnumerationOptions
                     {
                         IgnoreInaccessible = true, ReturnSpecialDirectories = false
                     }))
        {
            var child = GetTreeViewItem(folder);
            children.Add(child);
        }

        treeViewItem.Items = children;

        return treeViewItem;
    }
}
