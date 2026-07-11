using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Rendering;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageForumPostTemplates;

public partial class RenderForumPostDialog(IScopedOperationRunner operationRunner) : ComponentBase
{
    [Parameter]
    public int EntityId { get; set; }

    [Parameter]
    public ForumPostTemplateType Type { get; set; } = ForumPostTemplateType.Release;

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private IReadOnlyList<ForumPostTemplateSummaryReadModel> templates = [];
    private IReadOnlyList<string> renderErrors = [];
    private int selectedTemplateId;
    private string renderedContent = string.Empty;
    private bool isRendering;
    private string RenderedCopyTargetId => $"rendered-forum-post-{EntityId}";

    private IReadOnlyList<SelectOption<int>> TemplateOptions =>
        templates
            .Select(template => new SelectOption<int>(template.ForumPostTemplateId, template.Name))
            .ToList();

    protected override async Task OnInitializedAsync()
    {
        templates = await operationRunner.RunAsync(
            (IForumPostTemplateReadRepository repository) => repository.GetAllAsync(Type)
        );
        selectedTemplateId = templates.FirstOrDefault()?.ForumPostTemplateId ?? 0;

        if (selectedTemplateId != 0)
        {
            await RenderAsync();
        }
    }

    private async Task HandleTemplateChangedAsync(int value)
    {
        selectedTemplateId = value;
        await RenderAsync();
    }

    private string GetTemplateDisplayText(int templateId)
    {
        return templates
                .FirstOrDefault(template => template.ForumPostTemplateId == templateId)
                ?.Name
            ?? templateId.ToString();
    }

    private async Task RenderAsync()
    {
        if (selectedTemplateId == 0)
        {
            return;
        }

        isRendering = true;
        renderErrors = [];

        try
        {
            var result = await operationRunner.RunAsync(
                (ForumPostRenderService service) =>
                    service.RenderAsync(EntityId, selectedTemplateId)
            );
            renderedContent = result.Content;
            renderErrors = result.Errors;
        }
        catch (Exception exception)
        {
            renderedContent = string.Empty;
            renderErrors = [exception.Message];
        }
        finally
        {
            isRendering = false;
        }
    }

    private async Task CloseAsync()
    {
        await DialogRef.CancelAsync();
    }
}
