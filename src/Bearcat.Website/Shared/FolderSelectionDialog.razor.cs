using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Shared;

public partial class FolderSelectionDialog(IScopedOperationRunner operationRunner) : ComponentBase
{
    [Parameter]
    public IReadOnlyList<string> BaseFolderPaths { get; set; } = [];

    [Parameter]
    public IFolderSelectionSource? Source { get; set; }

    [Parameter]
    public string? SelectedFolderPath { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private IFolderSelectionSource source = null!;
    private string? selectedItem;
    private HashSet<string> expandedItems = [];
    private List<FolderSelectionNode> rootNodes = [];
    private bool isInitializing = true;
    private string? loadErrorMessage;

    protected override async Task OnInitializedAsync()
    {
        source = Source ?? new LocalFolderSelectionSource(operationRunner, BaseFolderPaths);
        rootNodes = source
            .RootPaths.Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(CreateRootNode)
            .ToList();

        try
        {
            await InitializeTreeAsync();
        }
        finally
        {
            isInitializing = false;
        }
    }

    private async Task InitializeTreeAsync()
    {
        if (rootNodes.Count == 0)
        {
            return;
        }

        if (await InitializeSelectionAsync())
        {
            return;
        }

        var firstRootNode = rootNodes[0];

        if (
            firstRootNode.ChildrenLoaded
            || (loadErrorMessage is null && await EnsureChildrenLoadedAsync(firstRootNode))
        )
        {
            expandedItems = [firstRootNode.Path];
        }
    }

    private async Task SaveAsync()
    {
        await DialogRef.CloseAsync(DialogResult.Ok(selectedItem));
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }

    private async Task<IEnumerable<FolderSelectionNode>> LoadChildrenForTreeAsync(
        FolderSelectionNode node
    )
    {
        var isLoaded = await EnsureChildrenLoadedAsync(node);
        StateHasChanged();

        if (!isLoaded)
        {
            throw new InvalidOperationException(loadErrorMessage);
        }

        return node.Children;
    }

    private async Task<bool> EnsureChildrenLoadedAsync(FolderSelectionNode node)
    {
        if (node.ChildrenLoaded)
        {
            return true;
        }

        var listing = await source.GetChildFoldersAsync(node.Path);

        if (!listing.IsSuccess)
        {
            loadErrorMessage = listing.ErrorMessage ?? string.Empty;

            return false;
        }

        loadErrorMessage = null;
        node.Children = listing.FolderPaths.Select(CreateNode).ToList();
        node.ChildrenLoaded = true;
        node.HasChildren = node.Children.Count > 0;

        return true;
    }

    private async Task<bool> InitializeSelectionAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedFolderPath))
        {
            return false;
        }

        var rootNode = FindRootContainingPath(SelectedFolderPath);

        if (rootNode is null)
        {
            return false;
        }

        var selectedNode = await LoadNodesDownToSelectedPathAsync(rootNode, SelectedFolderPath);

        if (selectedNode is null)
        {
            return false;
        }

        selectedItem = selectedNode.Path;
        expandedItems = GetAncestorPaths(selectedNode.Path, rootNode.Path)
            .Append(rootNode.Path)
            .ToHashSet();

        if (!await EnsureChildrenLoadedAsync(selectedNode))
        {
            expandedItems.Remove(selectedNode.Path);
        }

        return true;
    }

    private FolderSelectionNode? FindRootContainingPath(string path)
    {
        var normalizedPath = source.NormalizePath(path);

        if (normalizedPath is null)
        {
            return null;
        }

        return rootNodes.FirstOrDefault(root =>
            source.IsSameOrDescendantPath(normalizedPath, root.Path)
        );
    }

    private async Task<FolderSelectionNode?> LoadNodesDownToSelectedPathAsync(
        FolderSelectionNode rootNode,
        string selectedPath
    )
    {
        var normalizedSelectedPath = source.NormalizePath(selectedPath);

        if (
            normalizedSelectedPath is null
            || !source.IsSameOrDescendantPath(normalizedSelectedPath, rootNode.Path)
        )
        {
            return null;
        }

        var currentNode = rootNode;

        while (!PathsEqual(currentNode.Path, normalizedSelectedPath))
        {
            if (!await EnsureChildrenLoadedAsync(currentNode))
            {
                return null;
            }

            var nextNode = currentNode.Children.FirstOrDefault(child =>
                source.IsSameOrDescendantPath(normalizedSelectedPath, child.Path)
            );

            if (nextNode is null)
            {
                return null;
            }

            currentNode = nextNode;
        }

        return currentNode;
    }

    private List<string> GetAncestorPaths(string path, string basePath)
    {
        var ancestorPaths = new List<string>();
        var currentPath = source.NormalizePath(path);

        while (currentPath is not null && !PathsEqual(currentPath, basePath))
        {
            currentPath = source.GetParentPath(currentPath);

            if (currentPath is not null)
            {
                ancestorPaths.Add(currentPath);
            }
        }

        return ancestorPaths;
    }

    private bool PathsEqual(string first, string second)
    {
        return string.Equals(
            source.NormalizePath(first),
            source.NormalizePath(second),
            StringComparison.Ordinal
        );
    }

    private static FolderSelectionNode CreateRootNode(string path)
    {
        return new FolderSelectionNode { Path = path, Name = path };
    }

    private FolderSelectionNode CreateNode(string path)
    {
        return new FolderSelectionNode { Path = path, Name = source.GetDisplayName(path) };
    }
}
