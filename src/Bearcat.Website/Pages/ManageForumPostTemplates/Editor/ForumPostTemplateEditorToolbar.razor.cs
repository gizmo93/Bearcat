using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;
using Bearcat.Domain.ValueObjects;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageForumPostTemplates.Editor;

public partial class ForumPostTemplateEditorToolbar : ComponentBase
{
    [Parameter]
    public string Name { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> NameChanged { get; set; }

    [Parameter]
    public ForumPostTemplateType Type { get; set; }

    [Parameter]
    public EventCallback<ForumPostTemplateType> TypeChanged { get; set; }

    [Parameter]
    public ForumPostTemplateOutputFormat OutputFormat { get; set; }

    [Parameter]
    public EventCallback<ForumPostTemplateOutputFormat> OutputFormatChanged { get; set; }

    [Parameter]
    public bool IsNewTemplate { get; set; }

    [Parameter]
    public bool IsDirty { get; set; }

    [Parameter]
    public bool IsSaving { get; set; }

    [Parameter]
    public bool TemplateSheetOpen { get; set; }

    [Parameter]
    public EventCallback<bool> TemplateSheetOpenChanged { get; set; }

    [Parameter]
    public RenderFragment? TemplateList { get; set; }

    [Parameter]
    public IReadOnlyList<ForumPostTemplatePreviewEntityReadModel> PreviewEntities { get; set; } =
    [];

    [Parameter]
    public int? PreviewEntityId { get; set; }

    [Parameter]
    public EventCallback<int> OnPreviewEntitySelected { get; set; }

    [Parameter]
    public string PreviewEntitySearchTerm { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> PreviewEntitySearchTermChanged { get; set; }

    [Parameter]
    public EventCallback OnSave { get; set; }

    [Parameter]
    public EventCallback OnDuplicate { get; set; }

    [Parameter]
    public EventCallback OnDelete { get; set; }

    private IReadOnlyList<SelectOption<ForumPostTemplateType>> TypeOptions =>
        Enum.GetValues<ForumPostTemplateType>()
            .Select(type => new SelectOption<ForumPostTemplateType>(
                type,
                ForumPostTemplateTypeLabels.Get(L, type)
            ))
            .ToList();

    private IReadOnlyList<SelectOption<ForumPostTemplateOutputFormat>> OutputFormatOptions =>
        Enum.GetValues<ForumPostTemplateOutputFormat>()
            .Select(outputFormat => new SelectOption<ForumPostTemplateOutputFormat>(
                outputFormat,
                GetOutputFormatLabel(outputFormat)
            ))
            .ToList();

    private IReadOnlyList<SelectOption<int?>> PreviewEntityOptions =>
        PreviewEntities
            .Select(entity => new SelectOption<int?>(entity.EntityId, entity.Name))
            .ToList();

    private string NoPreviewEntitiesText =>
        Type == ForumPostTemplateType.ReleaseCollection
            ? L["NoReleaseCollectionsFound"]
            : L["NoReleasesFound"];

    private string SearchPreviewEntitiesText =>
        Type == ForumPostTemplateType.ReleaseCollection
            ? L["SearchReleaseCollections"]
            : L["SearchReleases"];

    private async Task SelectPreviewEntityAsync(int? entityId)
    {
        if (entityId is { } selectedEntityId)
        {
            await OnPreviewEntitySelected.InvokeAsync(selectedEntityId);
        }
    }

    private async Task ChangeNameAsync(ChangeEventArgs args)
    {
        await NameChanged.InvokeAsync(args.Value?.ToString() ?? string.Empty);
    }

    private string GetOutputFormatLabel(ForumPostTemplateOutputFormat outputFormat)
    {
        return outputFormat switch
        {
            ForumPostTemplateOutputFormat.BBCode => L["ForumPostTemplateOutputFormatBBCode"],
            ForumPostTemplateOutputFormat.PlainText => L["ForumPostTemplateOutputFormatPlainText"],
            _ => outputFormat.ToString(),
        };
    }
}
