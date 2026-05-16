using Bearcat.Abstractions;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Blueprint.Shared;

public partial class FolderSelectionDialog(IFileSystemService fileSystemService) : ComponentBase
{
    [Parameter]
    public string BaseFolderPath { get; set; } = null!;

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private string? selectedItem;
    private string? searchText;
    private List<FolderNode> rootNodes = [];

    private IEnumerable<FolderNode> filteredRootNodes => FilterNodes(rootNodes, searchText);

    protected override void OnInitialized()
    {
        rootNodes = [CreateNode(BaseFolderPath)];
    }

    private async Task SaveAsync()
    {
        await DialogRef.CloseAsync(DialogResult.Ok(selectedItem));
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }

    private FolderNode CreateNode(string path)
    {
        return new FolderNode
        {
            Path = path,
            Name = string.IsNullOrWhiteSpace(Path.GetFileName(path))
                ? path
                : Path.GetFileName(path),
            Children = fileSystemService.GetFoldersInPath(path).Select(CreateNode).ToList(),
        };
    }

    private static IEnumerable<FolderNode> FilterNodes(
        IEnumerable<FolderNode> nodes,
        string? search
    )
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return nodes;
        }

        var filtered = new List<FolderNode>();

        foreach (var node in nodes)
        {
            var matchingChildren = FilterNodes(node.Children, search).ToList();
            var isMatch = node.Name.Contains(search, StringComparison.OrdinalIgnoreCase);

            if (!isMatch && matchingChildren.Count == 0)
            {
                continue;
            }

            filtered.Add(
                new FolderNode
                {
                    Path = node.Path,
                    Name = node.Name,
                    Children = matchingChildren,
                }
            );
        }

        return filtered;
    }

    private sealed class FolderNode
    {
        public required string Path { get; init; }

        public required string Name { get; init; }

        public List<FolderNode> Children { get; init; } = [];
    }
}
