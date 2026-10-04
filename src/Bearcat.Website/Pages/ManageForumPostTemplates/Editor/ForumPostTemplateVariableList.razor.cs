using Bearcat.Domain.Shared.ForumPostRendering;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageForumPostTemplates.Editor;

public partial class ForumPostTemplateVariableList : ComponentBase
{
    [Parameter]
    public IReadOnlyList<ForumPostTemplateVariableReadModel> Variables { get; set; } = [];

    [Parameter]
    public string? SearchTerm { get; set; }

    [Parameter]
    public EventCallback<string?> SearchTermChanged { get; set; }

    private IReadOnlyList<ForumPostTemplateVariableReadModel> FilteredVariables
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SearchTerm))
            {
                return Variables;
            }

            var searchTerm = SearchTerm.Trim();
            return Variables
                .Where(variable =>
                    variable.Path.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                    || variable.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                )
                .ToList();
        }
    }
}
