namespace Bearcat.Website.Shared;

public sealed class FolderSelectionNode
{
    public required string Path { get; init; }

    public required string Name { get; init; }

    public bool HasChildren { get; set; } = true;

    public bool ChildrenLoaded { get; set; }

    public List<FolderSelectionNode> Children { get; set; } = [];
}
