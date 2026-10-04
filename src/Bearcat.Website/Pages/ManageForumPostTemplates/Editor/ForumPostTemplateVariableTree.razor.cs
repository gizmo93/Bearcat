using Bearcat.Domain.Shared.ForumPostRendering;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageForumPostTemplates.Editor;

public partial class ForumPostTemplateVariableTree : ComponentBase
{
    private IReadOnlyList<ForumPostTemplateVariableNode>? filteredVariables;
    private string? filteredSearchTerm;
    private bool hasFilterChanged;
    private IReadOnlyList<ForumPostTemplateVariableNode> visibleNodes = [];
    private HashSet<string> expandedValues = [];
    private HashSet<string> expandedValuesBeforeSearch = [];

    [Parameter]
    public IReadOnlyList<ForumPostTemplateVariableNode> Variables { get; set; } = [];

    [Parameter]
    public string? SearchTerm { get; set; }

    [Parameter]
    public EventCallback<string?> SearchTermChanged { get; set; }

    [Parameter]
    public EventCallback<ForumPostTemplateVariableInsertion> OnInsertionRequested { get; set; }

    protected override void OnParametersSet()
    {
        if (
            ReferenceEquals(filteredVariables, Variables)
            && string.Equals(filteredSearchTerm, SearchTerm, StringComparison.Ordinal)
        )
        {
            return;
        }

        var wasSearching = !string.IsNullOrWhiteSpace(filteredSearchTerm);
        var isSearching = !string.IsNullOrWhiteSpace(SearchTerm);

        if (isSearching && !wasSearching)
        {
            expandedValuesBeforeSearch = expandedValues;
        }

        var result = ForumPostTemplateVariableTreeFilter.Filter(Variables, SearchTerm);
        visibleNodes = result.Nodes;

        if (isSearching)
        {
            expandedValues = result.ExpandedKeys.ToHashSet();
        }
        else if (wasSearching)
        {
            expandedValues = expandedValuesBeforeSearch;
        }

        filteredVariables = Variables;
        filteredSearchTerm = SearchTerm;
        hasFilterChanged = true;
    }

    protected override bool ShouldRender()
    {
        return hasFilterChanged;
    }

    protected override void OnAfterRender(bool firstRender)
    {
        hasFilterChanged = false;
    }

    private void ChangeExpandedValues(HashSet<string> values)
    {
        expandedValues = values;
    }
}
