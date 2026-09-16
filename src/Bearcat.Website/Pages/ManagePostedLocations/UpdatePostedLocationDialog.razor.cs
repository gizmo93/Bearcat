using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManagePostedLocations;

public partial class UpdatePostedLocationDialog(IScopedOperationRunner operationRunner)
    : ComponentBase
{
    [Parameter]
    public string PostedUrl { get; set; } = string.Empty;

    [Parameter]
    public int? ForumPostTemplateId { get; set; }

    [Parameter]
    public ForumPostTemplateType TemplateType { get; set; } = ForumPostTemplateType.Release;

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private IReadOnlyList<ForumPostTemplateSummaryReadModel> templates = [];
    private int selectedTemplateId;

    private IReadOnlyList<SelectOption<int>> TemplateOptions =>
        templates
            .Select(template => new SelectOption<int>(template.ForumPostTemplateId, template.Name))
            .ToList();

    protected override async Task OnInitializedAsync()
    {
        if (ForumPostTemplateId is { } storedTemplateId)
        {
            selectedTemplateId = storedTemplateId;
            return;
        }

        templates = await operationRunner.RunAsync(
            (IForumPostTemplateReadRepository repository) => repository.GetAllAsync(TemplateType)
        );

        selectedTemplateId = templates.FirstOrDefault()?.ForumPostTemplateId ?? 0;
    }

    private async Task ConfirmAsync()
    {
        await DialogRef.CloseAsync(DialogResult.Ok(selectedTemplateId));
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
