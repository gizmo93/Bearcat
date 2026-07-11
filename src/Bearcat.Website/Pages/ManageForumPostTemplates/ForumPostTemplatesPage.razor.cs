using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageForumPostTemplates;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Rendering;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;

namespace Bearcat.Website.Pages.ManageForumPostTemplates;

public partial class ForumPostTemplatesPage(
    DialogService dialogService,
    IScopedOperationRunner operationRunner
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

    private IReadOnlyList<SelectOption<ForumPostTemplateType>> TypeOptions =>
        Enum.GetValues<ForumPostTemplateType>()
            .Select(type => new SelectOption<ForumPostTemplateType>(type, GetTypeLabel(type)))
            .ToList();

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
        await LoadTemplatesAsync(selectFirst: true);
    }

    private async Task LoadTemplatesAsync(bool selectFirst)
    {
        isLoading = true;

        try
        {
            templates = await operationRunner.RunAsync(
                (IForumPostTemplateReadRepository repository) => repository.GetAllAsync()
            );
            if (selectFirst && templates.Count > 0)
            {
                await SelectTemplateAsync(templates[0].ForumPostTemplateId);
            }
            else if (selectFirst)
            {
                CreateNew();
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

        var template = await operationRunner.RunAsync(
            (IForumPostTemplateReadRepository repository) =>
                repository.GetDetailAsync(forumPostTemplateId)
        );
        if (template is null)
        {
            return;
        }

        formModel = new ForumPostTemplateFormModel
        {
            ForumPostTemplateId = template.ForumPostTemplateId,
            Name = template.Name,
            Type = template.Type,
            TemplateBody = template.TemplateBody,
        };

        ReloadVariables();
    }

    private void CreateNew()
    {
        errorMessage = null;
        validationResult = null;
        formModel = new ForumPostTemplateFormModel
        {
            Name = string.Empty,
            Type = ForumPostTemplateType.Release,
            TemplateBody = GetDefaultTemplate(ForumPostTemplateType.Release),
        };

        ReloadVariables();
    }

    private void OnTypeChanged()
    {
        if (formModel.ForumPostTemplateId is null)
        {
            formModel.TemplateBody = GetDefaultTemplate(formModel.Type);
        }

        ReloadVariables();
    }

    private void ReloadVariables()
    {
        variables = operationRunner.Run(
            (ForumPostRenderService service) => service.GetVariables(formModel.Type)
        );
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
            var templateId = await operationRunner.RunAsync(
                (ForumPostTemplateService service) =>
                    service.CreateAsync(formModel.Name, formModel.Type, formModel.TemplateBody)
            );
            await LoadTemplatesAsync(selectFirst: false);
            await SelectTemplateAsync(templateId);
            return;
        }

        await operationRunner.RunAsync(
            (ForumPostTemplateService service) =>
                service.UpdateAsync(
                    formModel.ForumPostTemplateId.Value,
                    formModel.Name,
                    formModel.Type,
                    formModel.TemplateBody
                )
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

        await operationRunner.RunAsync(
            (ForumPostTemplateService service) =>
                service.DeleteAsync(formModel.ForumPostTemplateId.Value)
        );
        await LoadTemplatesAsync(selectFirst: true);
    }

    private string GetTemplateListItemClass(int templateId)
    {
        var selected = formModel.ForumPostTemplateId == templateId;
        return selected
            ? "bearcat-forum-template-list-item bearcat-forum-template-list-item-active"
            : "bearcat-forum-template-list-item";
    }

    private string GetTypeLabel(ForumPostTemplateType type)
    {
        return type switch
        {
            ForumPostTemplateType.Release => L["ForumPostTemplateTypeRelease"],
            ForumPostTemplateType.ReleaseCollection => L["ForumPostTemplateTypeReleaseCollection"],
            _ => type.ToString(),
        };
    }

    private static string GetDefaultTemplate(ForumPostTemplateType type)
    {
        return type switch
        {
            ForumPostTemplateType.ReleaseCollection => DefaultCollectionTemplate,
            _ => DefaultReleaseTemplate,
        };
    }

    private const string DefaultReleaseTemplate = """
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

    private const string DefaultCollectionTemplate = """
        [CENTER]
        [B]{{ series.title }}[/B]

        [IMG]{{ series.cover_url }}[/IMG]

        {{ series.description }}

        {{ for release in releases }}
        [B]{{ release.name }}[/B]
        {{ for upload in release.uploads }}
        {{ for crypter in upload.link_crypters }}
        [URL='{{ crypter.container_link }}']{{ upload.name }} - {{ crypter.name }}[/URL]
        {{ end }}
        {{ end }}

        {{ end }}
        [/CENTER]
        """;
}
