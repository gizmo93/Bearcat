using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;
using Bearcat.Domain.ValueObjects;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageForumPostTemplates.Editor;

public partial class ForumPostTemplateList : ComponentBase
{
    [Parameter]
    public IReadOnlyList<ForumPostTemplateSummaryReadModel> Templates { get; set; } = [];

    [Parameter]
    public bool IsLoading { get; set; }

    [Parameter]
    public int? SelectedTemplateId { get; set; }

    [Parameter]
    public ForumPostTemplateType? UnsavedTemplateType { get; set; }

    [Parameter]
    public string UnsavedTemplateName { get; set; } = string.Empty;

    [Parameter]
    public bool IsDirty { get; set; }

    [Parameter]
    public string? SearchTerm { get; set; }

    [Parameter]
    public EventCallback<string?> SearchTermChanged { get; set; }

    [Parameter]
    public EventCallback<int> OnTemplateSelected { get; set; }

    [Parameter]
    public EventCallback OnNewTemplateRequested { get; set; }

    private string UnsavedTemplateDisplayName =>
        string.IsNullOrWhiteSpace(UnsavedTemplateName)
            ? L["UntitledForumPostTemplate"]
            : UnsavedTemplateName;

    private IReadOnlyList<ForumPostTemplateSummaryReadModel> GetVisibleTemplates(
        ForumPostTemplateType type
    )
    {
        var searchTerm = SearchTerm?.Trim() ?? string.Empty;
        return Templates
            .Where(template =>
                template.Type == type
                && (
                    searchTerm.Length == 0
                    || template.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                )
            )
            .ToList();
    }

    private string GetRuleCountText(int ruleCount)
    {
        return ruleCount == 1
            ? L["ForumPostingRuleCountOne"]
            : L["ForumPostingRuleCount", ruleCount];
    }

    private static string GetTemplateListItemClass(bool selected)
    {
        return selected
            ? "bearcat-forum-template-editor-list-item bearcat-forum-template-editor-list-item-selected"
            : "bearcat-forum-template-editor-list-item";
    }
}
