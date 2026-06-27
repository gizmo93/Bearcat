using Bearcat.Abstractions;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Shared;

public partial class FolderSelectionDialog(IFileSystemService fileSystemService) : ComponentBase
{
    [Parameter]
    public IReadOnlyList<string> BaseFolderPaths { get; set; } = [];

    [Parameter]
    public string? SelectedFolderPath { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private string? selectedItem;
    private HashSet<string> expandedItems = [];
    private string? searchText;
    private List<FolderSelectionNode> rootNodes = [];

    private IEnumerable<FolderSelectionNode> filteredRootNodes =>
        FilterNodes(rootNodes, searchText);

    protected override void OnInitialized()
    {
        rootNodes = BaseFolderPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(CreateRootNode)
            .ToList();

        if (rootNodes.Count == 0)
        {
            return;
        }

        if (InitializeSelection())
        {
            return;
        }

        expandedItems = [rootNodes[0].Path];
        EnsureChildrenLoaded(rootNodes[0]);
    }

    private async Task SaveAsync()
    {
        await DialogRef.CloseAsync(DialogResult.Ok(selectedItem));
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }

    private Task SelectFolderAsync(FolderSelectionNode node)
    {
        selectedItem = node.Path;
        return Task.CompletedTask;
    }

    private Task ToggleFolderAsync(FolderSelectionNode node)
    {
        if (expandedItems.Contains(node.Path))
        {
            expandedItems.Remove(node.Path);
            return Task.CompletedTask;
        }

        EnsureChildrenLoaded(node);
        expandedItems.Add(node.Path);

        return Task.CompletedTask;
    }

    private List<FolderSelectionNode> EnsureChildrenLoaded(FolderSelectionNode node)
    {
        if (node.ChildrenLoaded)
        {
            return node.Children;
        }

        node.Children = fileSystemService.GetFoldersInPath(node.Path).Select(CreateNode).ToList();
        node.ChildrenLoaded = true;
        node.HasChildren = node.Children.Count > 0;

        return node.Children;
    }

    private bool InitializeSelection()
    {
        if (string.IsNullOrWhiteSpace(SelectedFolderPath))
        {
            return false;
        }

        var rootNode = FindOwningRoot(SelectedFolderPath);

        if (rootNode is null)
        {
            return false;
        }

        var selectedNode = EnsureSelectedPath(rootNode, SelectedFolderPath);

        if (selectedNode is null)
        {
            return false;
        }

        EnsureChildrenLoaded(selectedNode);
        selectedItem = selectedNode.Path;
        expandedItems = GetAncestorPaths(selectedNode.Path, rootNode.Path)
            .Append(rootNode.Path)
            .ToHashSet();

        return true;
    }

    private FolderSelectionNode? FindOwningRoot(string path)
    {
        var normalizedPath = NormalizePath(path);

        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return null;
        }

        return rootNodes.FirstOrDefault(root => IsSameOrDescendantPath(normalizedPath, root.Path));
    }

    private FolderSelectionNode? EnsureSelectedPath(
        FolderSelectionNode rootNode,
        string selectedPath
    )
    {
        var normalizedSelectedPath = NormalizePath(selectedPath);

        if (
            string.IsNullOrWhiteSpace(normalizedSelectedPath)
            || !IsSameOrDescendantPath(normalizedSelectedPath, rootNode.Path)
        )
        {
            return null;
        }

        var currentNode = rootNode;

        while (!PathsEqual(currentNode.Path, normalizedSelectedPath))
        {
            var nextNode = EnsureChildrenLoaded(currentNode)
                .FirstOrDefault(child =>
                    IsSameOrDescendantPath(normalizedSelectedPath, child.Path)
                );

            if (nextNode is null)
            {
                return null;
            }

            currentNode = nextNode;
        }

        return currentNode;
    }

    private static IEnumerable<string> GetAncestorPaths(string path, string basePath)
    {
        var normalizedBasePath = NormalizePath(basePath);
        var currentPath = NormalizePath(path);

        while (
            !string.IsNullOrWhiteSpace(currentPath) && !PathsEqual(currentPath, normalizedBasePath)
        )
        {
            currentPath = Path.GetDirectoryName(currentPath);

            if (string.IsNullOrWhiteSpace(currentPath))
            {
                yield break;
            }

            yield return currentPath;
        }
    }

    private static bool PathsEqual(string? first, string? second)
    {
        return string.Equals(NormalizePath(first), NormalizePath(second), StringComparison.Ordinal);
    }

    private static bool IsSameOrDescendantPath(string path, string ancestorPath)
    {
        var normalizedPath = NormalizePath(path);
        var normalizedAncestorPath = NormalizePath(ancestorPath);

        if (
            string.IsNullOrWhiteSpace(normalizedPath)
            || string.IsNullOrWhiteSpace(normalizedAncestorPath)
        )
        {
            return false;
        }

        if (string.Equals(normalizedPath, normalizedAncestorPath, StringComparison.Ordinal))
        {
            return true;
        }

        var ancestorPrefix = normalizedAncestorPath.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedAncestorPath
            : $"{normalizedAncestorPath}{Path.DirectorySeparatorChar}";

        return normalizedPath.StartsWith(ancestorPrefix, StringComparison.Ordinal);
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(path);
        var rootPath = Path.GetPathRoot(fullPath);

        return string.Equals(fullPath, rootPath, StringComparison.Ordinal)
            ? fullPath
            : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static IEnumerable<FolderSelectionNode> FilterNodes(
        IEnumerable<FolderSelectionNode> nodes,
        string? search
    )
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return nodes;
        }

        var filtered = new List<FolderSelectionNode>();

        foreach (var node in nodes)
        {
            var matchingChildren = FilterNodes(node.Children, search).ToList();
            var isMatch = node.Name.Contains(search, StringComparison.OrdinalIgnoreCase);

            if (!isMatch && matchingChildren.Count == 0)
            {
                continue;
            }

            filtered.Add(
                new FolderSelectionNode
                {
                    Path = node.Path,
                    Name = node.Name,
                    HasChildren = node.HasChildren,
                    ChildrenLoaded = true,
                    Children = matchingChildren,
                }
            );
        }

        return filtered;
    }

    private static FolderSelectionNode CreateRootNode(string path)
    {
        return new FolderSelectionNode { Path = path, Name = path };
    }

    private FolderSelectionNode CreateNode(string path)
    {
        return new FolderSelectionNode
        {
            Path = path,
            Name = string.IsNullOrWhiteSpace(Path.GetFileName(path))
                ? path
                : Path.GetFileName(path),
        };
    }
}
