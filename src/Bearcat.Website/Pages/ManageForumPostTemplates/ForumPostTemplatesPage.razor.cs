using Bearcat.Domain.UseCases.ManageForumPostTemplates;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Repositories;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using BlazorBlueprint.Components;

namespace Bearcat.Website.Pages.ManageForumPostTemplates;

public partial class ForumPostTemplatesPage(
    IForumPostTemplateReadRepository readRepository,
    ForumPostTemplateService service,
    ForumPostRenderService renderService,
    DialogService dialogService
)
{
    private IReadOnlyList<ForumPostTemplateSummaryReadModel> templates = [];
    private IReadOnlyList<ForumPostTemplateVariableReadModel> variables = [];
    private ForumPostTemplateFormModel formModel = new();
    private ForumPostTemplateValidationResult? validationResult;
    private string? errorMessage;
    private bool isLoading;
    private bool templatesPanelOpen;
    private bool variablesPanelOpen;
    private string variableSearchTerm = string.Empty;

    private IReadOnlyList<ForumPostTemplateVariableReadModel> FilteredVariables
    {
        get
        {
            if (string.IsNullOrWhiteSpace(variableSearchTerm))
            {
                return variables;
            }

            var searchTerm = variableSearchTerm.Trim();
            return variables
                .Where(variable =>
                    variable.Path.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                    || variable.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                )
                .ToList();
        }
    }

    protected override async Task OnInitializedAsync()
    {
        variables = renderService.GetVariables();
        await LoadTemplatesAsync(selectFirst: true);
    }

    private async Task LoadTemplatesAsync(bool selectFirst)
    {
        isLoading = true;

        try
        {
            templates = await readRepository.GetAllAsync();
            if (selectFirst && templates.Count > 0)
            {
                await SelectTemplateAsync(templates[0].ForumPostTemplateId);
            }
            else if (selectFirst)
            {
                await CreateNewAsync();
            }
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task SelectTemplateAsync(int forumPostTemplateId)
    {
        errorMessage = null;
        validationResult = null;

        var template = await readRepository.GetDetailAsync(forumPostTemplateId);
        if (template is null)
        {
            return;
        }

        formModel = new ForumPostTemplateFormModel
        {
            ForumPostTemplateId = template.ForumPostTemplateId,
            Name = template.Name,
            TemplateBody = template.TemplateBody,
        };
    }

    private Task CreateNewAsync()
    {
        errorMessage = null;
        validationResult = null;
        formModel = new ForumPostTemplateFormModel
        {
            Name = string.Empty,
            TemplateBody = DefaultTemplate,
        };

        return Task.CompletedTask;
    }

    private Task ValidateAsync()
    {
        errorMessage = null;
        validationResult = ForumPostTemplateService.Validate(formModel.TemplateBody);
        return Task.CompletedTask;
    }

    private async Task SaveAsync()
    {
        errorMessage = null;
        validationResult = ForumPostTemplateService.Validate(formModel.TemplateBody);

        if (string.IsNullOrWhiteSpace(formModel.Name))
        {
            errorMessage = L["NameIsRequired"];
            return;
        }

        if (!validationResult.IsValid)
        {
            return;
        }

        if (formModel.ForumPostTemplateId is null)
        {
            var templateId = await service.CreateAsync(formModel.Name, formModel.TemplateBody);
            await LoadTemplatesAsync(selectFirst: false);
            await SelectTemplateAsync(templateId);
            return;
        }

        await service.UpdateAsync(
            formModel.ForumPostTemplateId.Value,
            formModel.Name,
            formModel.TemplateBody
        );
        await LoadTemplatesAsync(selectFirst: false);
        await SelectTemplateAsync(formModel.ForumPostTemplateId.Value);
    }

    private async Task DeleteAsync()
    {
        if (formModel.ForumPostTemplateId is null)
        {
            return;
        }

        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", formModel.Name],
            L["DeleteForumPostTemplateConfirmation", formModel.Name],
            new ConfirmDialogOptions
            {
                ConfirmText = L["Delete"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        if (!result.Confirmed)
        {
            return;
        }

        await service.DeleteAsync(formModel.ForumPostTemplateId.Value);
        await LoadTemplatesAsync(selectFirst: true);
    }

    private string GetTemplateListItemClass(int templateId)
    {
        var selected = formModel.ForumPostTemplateId == templateId;
        return selected
            ? "bearcat-forum-template-list-item bearcat-forum-template-list-item-active"
            : "bearcat-forum-template-list-item";
    }

    private const string DefaultTemplate = """
        [CENTER]
        [B]{{ release.name }}[/B]

        [SPOILER="NFO"]
        {{ release.nfo }}
        [/SPOILER]

        {{ for upload in uploads }}
        [B]{{ upload.name }}[/B]
        {{ for crypter in upload.link_crypters }}
        [URL='{{ crypter.container_link }}']{{ crypter.name }}[/URL]
        {{ end }}

        {{ end }}
        [/CENTER]
        """;
}
