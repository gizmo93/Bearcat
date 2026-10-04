using System.Globalization;
using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageForumPostTemplates;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Rendering;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.JSInterop;

namespace Bearcat.Website.Pages.ManageForumPostTemplates;

public partial class ForumPostTemplatesPage(
    DialogService dialogService,
    IScopedOperationRunner operationRunner,
    IJSRuntime js,
    PersistentComponentState applicationState,
    IHttpContextAccessor httpContextAccessor,
    ClientPlatform clientPlatform
) : IAsyncDisposable
{
    private const string PanelSizesKey = "bearcat.forumPostTemplates.panelSizes";
    private const string SidebarPanelSizesKey = "bearcat.forumPostTemplates.sidebarPanelSizes";
    private const string CursorPositionElementId = "forum-post-template-cursor-position";
    private const string EditorMobilePane = "editor";
    private const string PreviewMobilePane = "preview";
    private const string VariablesMobilePane = "variables";

    private static readonly double[] DefaultPanelSizes = [20, 40, 40];
    private static readonly double[] DefaultSidebarPanelSizes = [60, 40];

    private IReadOnlyList<ForumPostTemplateSummaryReadModel> templates = [];
    private IReadOnlyList<ForumPostTemplateVariableReadModel> variables = [];
    private ForumPostTemplateFormModel formModel = new();
    private EditorValues savedValues = new(
        string.Empty,
        ForumPostTemplateType.Release,
        ForumPostTemplateOutputFormat.BBCode,
        string.Empty
    );
    private ForumPostTemplateValidationResult validationResult = new(true, []);
    private string? saveErrorMessage;
    private bool isLoading;
    private bool isSaving;
    private bool templateSheetOpen;
    private bool pageLeft;
    private string? mobilePane = EditorMobilePane;
    private string? templateSearchTerm;
    private string? variableSearchTerm;
    private IReadOnlyList<double> panelSizes = DefaultPanelSizes;
    private IReadOnlyList<double> sidebarPanelSizes = DefaultSidebarPanelSizes;
    private PersistingComponentStateSubscription persistSubscription;
    private DotNetObjectReference<ForumPostTemplatesPage>? dotNetReference;
    private IJSObjectReference? saveShortcutHandle;

    private bool IsNewTemplate => formModel.ForumPostTemplateId is null;

    private bool IsDirty => CurrentValues != savedValues;

    private EditorValues CurrentValues =>
        new(formModel.Name, formModel.Type, formModel.OutputFormat, formModel.TemplateBody);

    private string SaveShortcutKeys => clientPlatform.IsMac ? "⌘S" : "Ctrl S";

    private string CursorPositionFormat => L["CursorPosition"];

    private string InitialCursorPosition =>
        string.Format(CultureInfo.CurrentCulture, CursorPositionFormat, 1, 1);

    private string CharacterCountText =>
        formModel.TemplateBody.Length == 1
            ? L["CharacterCountOne"]
            : L[
                "CharacterCount",
                formModel.TemplateBody.Length.ToString("N0", CultureInfo.CurrentCulture)
            ];

    private string SyntaxErrorCountText =>
        validationResult.Errors.Count == 1
            ? L["SyntaxErrorCountOne"]
            : L["SyntaxErrorCount", validationResult.Errors.Count];

    private string UnsavedTemplateDisplayName =>
        string.IsNullOrWhiteSpace(formModel.Name) ? L["UntitledForumPostTemplate"] : formModel.Name;

    private IReadOnlyList<SelectOption<ForumPostTemplateType>> TypeOptions =>
        Enum.GetValues<ForumPostTemplateType>()
            .Select(type => new SelectOption<ForumPostTemplateType>(type, GetTypeLabel(type)))
            .ToList();

    private IReadOnlyList<SelectOption<ForumPostTemplateOutputFormat>> OutputFormatOptions =>
        Enum.GetValues<ForumPostTemplateOutputFormat>()
            .Select(outputFormat => new SelectOption<ForumPostTemplateOutputFormat>(
                outputFormat,
                GetOutputFormatLabel(outputFormat)
            ))
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
        persistSubscription = applicationState.RegisterOnPersisting(PersistPanelSizes);
        panelSizes =
            RestorePanelSizes(PanelSizesKey, DefaultPanelSizes.Length) ?? DefaultPanelSizes;
        sidebarPanelSizes =
            RestorePanelSizes(SidebarPanelSizesKey, DefaultSidebarPanelSizes.Length)
            ?? DefaultSidebarPanelSizes;

        await LoadTemplatesAsync(selectFirst: true);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        dotNetReference = DotNetObjectReference.Create(this);

        try
        {
            saveShortcutHandle = await js.InvokeAsync<IJSObjectReference>(
                "bearcat.saveShortcut.attach",
                dotNetReference
            );
        }
        catch (JSDisconnectedException) { }
    }

    [JSInvokable]
    public async Task SaveFromShortcutAsync()
    {
        if (!IsDirty || isSaving)
        {
            return;
        }

        await SaveAsync();
        StateHasChanged();
    }

    private IReadOnlyList<double>? RestorePanelSizes(string key, int expectedPanelCount)
    {
        if (
            applicationState.TryTakeFromJson<double[]>(key, out var persisted)
            && persisted is not null
        )
        {
            return persisted;
        }

        return
            httpContextAccessor.HttpContext?.Request.Cookies.TryGetValue(key, out var cookie)
            == true
            ? PanelSizesCookieValue.Parse(cookie, expectedPanelCount)
            : null;
    }

    private Task PersistPanelSizes()
    {
        applicationState.PersistAsJson(PanelSizesKey, panelSizes.ToArray());
        applicationState.PersistAsJson(SidebarPanelSizesKey, sidebarPanelSizes.ToArray());
        return Task.CompletedTask;
    }

    private async Task SavePanelSizesAsync(PanelResizeEventArgs args)
    {
        panelSizes = args.Sizes.ToList();
        await SetCookieAsync(PanelSizesKey, PanelSizesCookieValue.Format(args.Sizes));
    }

    private async Task SaveSidebarPanelSizesAsync(PanelResizeEventArgs args)
    {
        sidebarPanelSizes = args.Sizes.ToList();
        await SetCookieAsync(SidebarPanelSizesKey, PanelSizesCookieValue.Format(args.Sizes));
    }

    private async Task SetCookieAsync(string key, string value)
    {
        try
        {
            await js.InvokeVoidAsync("bearcat.setCookie", key, value);
        }
        catch (JSException) { }
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
                await LoadTemplateIntoEditorAsync(templates[0].ForumPostTemplateId);
            }
            else if (selectFirst)
            {
                StartNewTemplate();
            }
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task SelectTemplateAsync(int forumPostTemplateId)
    {
        templateSheetOpen = false;

        if (formModel.ForumPostTemplateId == forumPostTemplateId)
        {
            return;
        }

        if (!await ConfirmDiscardChangesAsync())
        {
            return;
        }

        await LoadTemplateIntoEditorAsync(forumPostTemplateId);
    }

    private async Task CreateNewTemplateAsync()
    {
        templateSheetOpen = false;

        if (!await ConfirmDiscardChangesAsync())
        {
            return;
        }

        StartNewTemplate();
    }

    private async Task<bool> ConfirmDiscardChangesAsync()
    {
        if (!IsDirty)
        {
            return true;
        }

        var result = await dialogService.ConfirmAsync(
            L["DiscardChangesTitle"],
            L["DiscardChangesMessage"],
            new ConfirmDialogOptions
            {
                ConfirmText = L["DiscardChanges"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        return result.Confirmed;
    }

    private async Task LoadTemplateIntoEditorAsync(int forumPostTemplateId)
    {
        var template = await operationRunner.RunAsync(
            (IForumPostTemplateReadRepository repository) =>
                repository.GetDetailAsync(forumPostTemplateId)
        );
        if (template is null)
        {
            return;
        }

        ShowInEditor(
            new ForumPostTemplateFormModel
            {
                ForumPostTemplateId = template.ForumPostTemplateId,
                Name = template.Name,
                Type = template.Type,
                OutputFormat = template.OutputFormat,
                TemplateBody = template.TemplateBody,
            }
        );
    }

    private void StartNewTemplate()
    {
        ShowInEditor(
            new ForumPostTemplateFormModel
            {
                Name = string.Empty,
                Type = ForumPostTemplateType.Release,
                OutputFormat = ForumPostTemplateOutputFormat.BBCode,
                TemplateBody = GetDefaultTemplate(ForumPostTemplateType.Release),
            }
        );
    }

    private void ShowInEditor(ForumPostTemplateFormModel model)
    {
        formModel = model;
        savedValues = CurrentValues;
        saveErrorMessage = null;
        ValidateTemplateBody();
        ReloadVariables();
    }

    private void ChangeNewTemplateType(ForumPostTemplateType type)
    {
        formModel.Type = type;
        formModel.TemplateBody = GetDefaultTemplate(type);
        ValidateTemplateBody();
        ReloadVariables();
    }

    private void ChangeName(ChangeEventArgs args)
    {
        formModel.Name = args.Value?.ToString() ?? string.Empty;
    }

    private void ChangeTemplateBody(string? templateBody)
    {
        formModel.TemplateBody = templateBody ?? string.Empty;
        ValidateTemplateBody();
    }

    private void ValidateTemplateBody()
    {
        validationResult = ForumPostTemplateService.Validate(formModel.TemplateBody);
    }

    private void ReloadVariables()
    {
        variables = operationRunner.Run(
            (ForumPostRenderService service) => service.GetVariables(formModel.Type)
        );
    }

    private async Task SaveAsync()
    {
        isSaving = true;

        try
        {
            await SaveTemplateAsync();
        }
        finally
        {
            isSaving = false;
        }
    }

    private async Task SaveTemplateAsync()
    {
        saveErrorMessage = null;
        var name = formModel.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            saveErrorMessage = L["NameIsRequired"];
            return;
        }

        if (IsNameTakenByOtherTemplate(name))
        {
            saveErrorMessage = L["ForumPostTemplateNameAlreadyExists", name];
            return;
        }

        if (!validationResult.IsValid)
        {
            saveErrorMessage = L["FixTemplateSyntaxErrorsBeforeSaving"];
            return;
        }

        var templateId = formModel.ForumPostTemplateId;
        if (templateId is null)
        {
            templateId = await operationRunner.RunAsync(
                (ForumPostTemplateService service) =>
                    service.CreateAsync(
                        name,
                        formModel.Type,
                        formModel.OutputFormat,
                        formModel.TemplateBody
                    )
            );
        }
        else
        {
            await operationRunner.RunAsync(
                (ForumPostTemplateService service) =>
                    service.UpdateAsync(
                        templateId.Value,
                        name,
                        formModel.OutputFormat,
                        formModel.TemplateBody
                    )
            );
        }

        await LoadTemplatesAsync(selectFirst: false);
        await LoadTemplateIntoEditorAsync(templateId.Value);
    }

    private bool IsNameTakenByOtherTemplate(string name)
    {
        return templates.Any(template =>
            template.ForumPostTemplateId != formModel.ForumPostTemplateId
            && string.Equals(template.Name, name, StringComparison.Ordinal)
        );
    }

    private async Task DuplicateAsync()
    {
        if (!await ConfirmDiscardChangesAsync())
        {
            return;
        }

        var sourceTemplate = GetSelectedTemplateSummary();
        var copyName = ForumPostTemplateCopyNameGenerator.CreateCopyName(
            sourceTemplate.Name,
            templates.Select(template => template.Name).ToList(),
            L["ForumPostTemplateCopyName"],
            L["ForumPostTemplateNumberedCopyName"]
        );

        var copyId = await operationRunner.RunAsync(
            (ForumPostTemplateService service) =>
                service.DuplicateAsync(sourceTemplate.ForumPostTemplateId, copyName)
        );
        await LoadTemplatesAsync(selectFirst: false);
        await LoadTemplateIntoEditorAsync(copyId);
    }

    private async Task DeleteAsync()
    {
        var template = GetSelectedTemplateSummary();

        if (template.ForumPostingRuleCount > 0)
        {
            await dialogService.AlertAsync(
                L["ForumPostTemplateCannotBeDeleted"],
                template.ForumPostingRuleCount == 1
                    ? L["ForumPostTemplateUsedByOnePostingRule", template.Name]
                    : L[
                        "ForumPostTemplateUsedByPostingRules",
                        template.Name,
                        template.ForumPostingRuleCount
                    ],
                new AlertDialogOptions { ButtonText = L["Close"] }
            );
            return;
        }

        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", template.Name],
            L["DeleteForumPostTemplateConfirmation", template.Name],
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
            (ForumPostTemplateService service) => service.DeleteAsync(template.ForumPostTemplateId)
        );
        await LoadTemplatesAsync(selectFirst: true);
    }

    private ForumPostTemplateSummaryReadModel GetSelectedTemplateSummary()
    {
        return templates.Single(template =>
            template.ForumPostTemplateId == formModel.ForumPostTemplateId
        );
    }

    private IReadOnlyList<ForumPostTemplateSummaryReadModel> GetVisibleTemplates(
        ForumPostTemplateType type
    )
    {
        var searchTerm = templateSearchTerm?.Trim() ?? string.Empty;
        return templates
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

    private string GetTypeLabel(ForumPostTemplateType type)
    {
        return type switch
        {
            ForumPostTemplateType.Release => L["ForumPostTemplateTypeRelease"],
            ForumPostTemplateType.ReleaseCollection => L["ForumPostTemplateTypeReleaseCollection"],
            _ => type.ToString(),
        };
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

    public async ValueTask DisposeAsync()
    {
        persistSubscription.Dispose();

        if (saveShortcutHandle is not null)
        {
            try
            {
                await saveShortcutHandle.InvokeVoidAsync("detach");
                await saveShortcutHandle.DisposeAsync();
            }
            catch (JSDisconnectedException) { }
        }

        dotNetReference?.Dispose();
    }

    private static string GetDefaultTemplate(ForumPostTemplateType type)
    {
        return type switch
        {
            ForumPostTemplateType.ReleaseCollection => DefaultCollectionTemplate,
            _ => DefaultReleaseTemplate,
        };
    }

    private sealed record EditorValues(
        string Name,
        ForumPostTemplateType Type,
        ForumPostTemplateOutputFormat OutputFormat,
        string TemplateBody
    );

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
