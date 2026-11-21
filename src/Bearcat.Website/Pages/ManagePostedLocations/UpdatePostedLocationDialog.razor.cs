using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.ManageForumPostTemplates.Editor;
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
    public int DistributionSiteRegistrationId { get; set; }

    [Parameter]
    public int? ForumPostTemplateId { get; set; }

    [Parameter]
    public ForumPostTemplateType TemplateType { get; set; } = ForumPostTemplateType.Release;

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private IReadOnlyList<ForumPostTemplateSummaryReadModel> templates = [];
    private string noTemplatesMessage = string.Empty;
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

        var registration = await operationRunner.RunAsync(
            (IDistributionSiteRegistrationReadRepository repository) =>
                repository.GetByIdAsync(DistributionSiteRegistrationId)
        );

        templates = await operationRunner.RunAsync(
            (IForumPostTemplateReadRepository repository) =>
                repository.GetAllAsync(
                    TemplateType,
                    outputFormat: registration!.TemplateOutputFormat
                )
        );

        noTemplatesMessage = L[
            "NoMatchingForumPostTemplateFound",
            ForumPostTemplateTypeLabels.Get(L, TemplateType),
            L[$"ForumPostTemplateOutputFormat{registration!.TemplateOutputFormat}"]
        ];

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
