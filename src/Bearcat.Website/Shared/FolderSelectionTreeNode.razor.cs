using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Shared;

public partial class FolderSelectionTreeNode : ComponentBase
{
    [Parameter, EditorRequired]
    public FolderSelectionNode Node { get; set; } = null!;

    [Parameter]
    public int Depth { get; set; }

    [Parameter]
    public string? SelectedPath { get; set; }

    [Parameter]
    public HashSet<string> ExpandedItems { get; set; } = [];

    [Parameter]
    public EventCallback<FolderSelectionNode> OnSelect { get; set; }

    [Parameter]
    public EventCallback<FolderSelectionNode> OnToggle { get; set; }

    private bool IsExpanded => ExpandedItems.Contains(Node.Path);

    private bool IsSelected => string.Equals(SelectedPath, Node.Path, StringComparison.Ordinal);

    private string? AriaExpanded =>
        Node.HasChildren ? IsExpanded.ToString().ToLowerInvariant() : null;

    private string AriaSelected => IsSelected.ToString().ToLowerInvariant();

    private string RowClass =>
        "bearcat-folder-tree-node"
        + (IsSelected ? " bearcat-folder-tree-node-selected" : string.Empty);

    private string RowStyle =>
        "display: block;"
        + " position: relative;"
        + " min-width: 0;"
        + " width: 100%;"
        + " border-radius: 0.375rem;"
        + " padding-top: 0.25rem;"
        + " padding-bottom: 0.25rem;"
        + " padding-right: 0.5rem;"
        + $" padding-left: {FormatRem(2.125 + (Depth * 1.25))};"
        + " cursor: pointer;"
        + " user-select: none;"
        + (
            IsSelected
                ? " background-color: var(--accent); color: var(--accent-foreground);"
                : string.Empty
        );

    private string ToggleStyle =>
        "display: inline-flex;"
        + " position: absolute;"
        + $" left: {FormatRem(0.5 + (Depth * 1.25))};"
        + " top: 50%;"
        + " transform: translateY(-50%);"
        + " width: 1.25rem;"
        + " min-width: 1.25rem;"
        + " height: 1.25rem;"
        + " align-items: center;"
        + " justify-content: center;";

    private static string LabelStyle => "display: block; min-width: 0;";

    private static string FormatRem(double value)
    {
        return $"{value.ToString("0.###", CultureInfo.InvariantCulture)}rem";
    }

    private string ChevronClass =>
        "h-4 w-4 shrink-0 text-muted-foreground transition-transform duration-200"
        + (IsExpanded ? " rotate-90" : string.Empty);

    private Task SelectAsync()
    {
        return OnSelect.InvokeAsync(Node);
    }

    private Task ToggleAsync()
    {
        return OnToggle.InvokeAsync(Node);
    }
}
