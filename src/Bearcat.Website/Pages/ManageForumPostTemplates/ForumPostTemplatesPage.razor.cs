using System.Diagnostics;
using System.Globalization;
using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageForumPostTemplates;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Rendering;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Rendering.Preview;
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
    IHttpContextAccessor httpContextAccessor
) : IAsyncDisposable
{
    private const string PanelSizesKey = "bearcat.forumPostTemplates.panelSizes";
    private const string SidebarPanelSizesKey = "bearcat.forumPostTemplates.sidebarPanelSizes";
    private const string CursorPositionElementId = "forum-post-template-cursor-position";
    private const string EditorMobilePane = "editor";
    private const string PreviewMobilePane = "preview";
    private const string VariablesMobilePane = "variables";
    private const string PreviewEntityKeyPrefix = "bearcat.forumPostTemplates.previewEntity.";
    private const int PreviewEntitySearchLimit = 20;

    private static readonly TimeSpan PreviewRenderDelay = TimeSpan.FromMilliseconds(300);

    private static readonly double[] DefaultPanelSizes = [20, 40, 40];
    private static readonly double[] DefaultSidebarPanelSizes = [60, 40];

    private IReadOnlyList<ForumPostTemplateSummaryReadModel> templates = [];
    private IReadOnlyList<ForumPostTemplateVariableNode> variables = [];
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
    private LineNumberedTextarea templateBodyEditor = null!;
    private ForumPostTemplateError? pendingEditorFocusError;
    private ForumPostTemplateVariableInsertion? pendingEditorInsertion;
    private readonly Dictionary<ForumPostTemplateType, int> previewEntityIds = [];
    private IReadOnlyList<ForumPostTemplatePreviewEntityReadModel> previewEntities = [];
    private ForumPostTemplatePreviewEntityReadModel? previewEntity;
    private string previewEntitySearchTerm = string.Empty;
    private ForumPostTemplateType? previewDataType;
    private ForumPostTemplatePreviewData? previewData;
    private bool isPreviewLoading;
    private string? previewContent;
    private IReadOnlyList<ForumPostTemplateError> previewErrors = [];
    private TimeSpan? previewRenderDuration;
    private readonly RestartableDelay previewRenderDelay = new();

    private bool IsNewTemplate => formModel.ForumPostTemplateId is null;

    private bool IsDirty => CurrentValues != savedValues;

    private EditorValues CurrentValues =>
        new(formModel.Name, formModel.Type, formModel.OutputFormat, formModel.TemplateBody);

    private string CursorPositionFormat => L["CursorPosition"];

    private IReadOnlyList<int> ErrorLineNumbers =>
        validationResult
            .Errors.Concat(previewErrors)
            .Select(error => error.Line)
            .OfType<int>()
            .Distinct()
            .Order()
            .ToList();

    protected override async Task OnInitializedAsync()
    {
        persistSubscription = applicationState.RegisterOnPersisting(PersistUiState);
        RestorePreviewEntityIds();
        panelSizes =
            RestorePanelSizes(PanelSizesKey, DefaultPanelSizes.Length) ?? DefaultPanelSizes;
        sidebarPanelSizes =
            RestorePanelSizes(SidebarPanelSizesKey, DefaultSidebarPanelSizes.Length)
            ?? DefaultSidebarPanelSizes;

        await LoadTemplatesAsync(selectFirst: true);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (pendingEditorFocusError is { Line: { } line } focusError)
        {
            pendingEditorFocusError = null;

            try
            {
                await templateBodyEditor.FocusPositionAsync(line, focusError.Column ?? 1);
            }
            catch (JSDisconnectedException) { }
        }

        if (pendingEditorInsertion is { } insertion)
        {
            pendingEditorInsertion = null;

            try
            {
                await templateBodyEditor.InsertTextAsync(insertion.Text, insertion.CursorOffset);
            }
            catch (JSDisconnectedException) { }
        }

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

    private void RestorePreviewEntityIds()
    {
        foreach (var type in Enum.GetValues<ForumPostTemplateType>())
        {
            if (RestorePreviewEntityId(GetPreviewEntityKey(type)) is { } entityId)
            {
                previewEntityIds[type] = entityId;
            }
        }
    }

    private int? RestorePreviewEntityId(string key)
    {
        if (applicationState.TryTakeFromJson<int>(key, out var persisted))
        {
            return persisted;
        }

        return
            httpContextAccessor.HttpContext?.Request.Cookies.TryGetValue(key, out var cookie)
                == true
            && int.TryParse(
                cookie,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var entityId
            )
            ? entityId
            : null;
    }

    private static string GetPreviewEntityKey(ForumPostTemplateType type)
    {
        return PreviewEntityKeyPrefix + type;
    }

    private Task PersistUiState()
    {
        applicationState.PersistAsJson(PanelSizesKey, panelSizes.ToArray());
        applicationState.PersistAsJson(SidebarPanelSizesKey, sidebarPanelSizes.ToArray());

        foreach (var (type, entityId) in previewEntityIds)
        {
            applicationState.PersistAsJson(GetPreviewEntityKey(type), entityId);
        }

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
                await StartNewTemplateAsync();
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

        await StartNewTemplateAsync();
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

        await ShowInEditorAsync(
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

    private async Task StartNewTemplateAsync()
    {
        await ShowInEditorAsync(
            new ForumPostTemplateFormModel
            {
                Name = string.Empty,
                Type = ForumPostTemplateType.Release,
                OutputFormat = ForumPostTemplateOutputFormat.BBCode,
                TemplateBody = GetDefaultTemplate(ForumPostTemplateType.Release),
            }
        );
    }

    private async Task ShowInEditorAsync(ForumPostTemplateFormModel model)
    {
        formModel = model;
        savedValues = CurrentValues;
        saveErrorMessage = null;
        ValidateTemplateBody();
        ReloadVariables();
        await ShowPreviewForCurrentTemplateAsync();
    }

    private async Task ChangeNewTemplateTypeAsync(ForumPostTemplateType type)
    {
        formModel.Type = type;
        formModel.TemplateBody = GetDefaultTemplate(type);
        ValidateTemplateBody();
        ReloadVariables();
        await ShowPreviewForCurrentTemplateAsync();
    }

    private void ChangeName(string name)
    {
        formModel.Name = name;
    }

    private void ChangeOutputFormat(ForumPostTemplateOutputFormat outputFormat)
    {
        formModel.OutputFormat = outputFormat;
    }

    private void ChangeTemplateBody(string? templateBody)
    {
        formModel.TemplateBody = templateBody ?? string.Empty;
        ValidateTemplateBody();
        SchedulePreviewRender();
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

    private async Task ShowPreviewForCurrentTemplateAsync()
    {
        previewRenderDelay.Cancel();
        previewContent = null;
        previewErrors = [];
        previewRenderDuration = null;

        var type = formModel.Type;
        if (previewDataType != type)
        {
            previewDataType = null;
            previewData = null;
            previewEntity = null;
            previewEntities = [];
            isPreviewLoading = true;
            var entity = await ResolvePreviewEntityAsync(type);
            await LoadPreviewDataAsync(type, entity);
        }

        await RenderPreviewNowAsync();
    }

    private async Task<ForumPostTemplatePreviewEntityReadModel?> ResolvePreviewEntityAsync(
        ForumPostTemplateType type
    )
    {
        if (previewEntityIds.TryGetValue(type, out var entityId))
        {
            var rememberedEntity = await operationRunner.RunAsync(
                (IForumPostTemplatePreviewEntityReadRepository repository) =>
                    repository.GetAsync(type, entityId)
            );
            if (rememberedEntity is not null)
            {
                return rememberedEntity;
            }
        }

        var newestEntities = await operationRunner.RunAsync(
            (IForumPostTemplatePreviewEntityReadRepository repository) =>
                repository.SearchAsync(type, searchTerm: null, limit: 1)
        );

        return newestEntities.FirstOrDefault();
    }

    private async Task LoadPreviewDataAsync(
        ForumPostTemplateType type,
        ForumPostTemplatePreviewEntityReadModel? entity
    )
    {
        isPreviewLoading = true;

        try
        {
            var loadedPreviewData = entity is null
                ? null
                : await operationRunner.RunAsync(
                    (ForumPostRenderService service) =>
                        service.LoadPreviewDataAsync(type, entity.EntityId)
                );
            var loadedEntities = loadedPreviewData is null
                ? []
                : await operationRunner.RunAsync(
                    (IForumPostTemplatePreviewEntityReadRepository repository) =>
                        repository.SearchAsync(type, searchTerm: null, PreviewEntitySearchLimit)
                );

            if (formModel.Type != type)
            {
                return;
            }

            previewDataType = type;
            previewData = loadedPreviewData;
            previewEntity = loadedPreviewData is null ? null : entity;
            previewEntitySearchTerm = string.Empty;
            previewEntities = IncludeSelectedPreviewEntity(loadedEntities);

            if (previewEntity is not null)
            {
                previewEntityIds[type] = previewEntity.EntityId;
            }
        }
        finally
        {
            isPreviewLoading = false;
        }
    }

    private async Task SelectPreviewEntityAsync(int entityId)
    {
        if (previewEntity?.EntityId == entityId)
        {
            return;
        }

        var type = formModel.Type;
        var entity = previewEntities.Single(candidate => candidate.EntityId == entityId);
        await SetCookieAsync(
            GetPreviewEntityKey(type),
            entityId.ToString(CultureInfo.InvariantCulture)
        );

        previewRenderDelay.Cancel();
        await LoadPreviewDataAsync(type, entity);
        await RenderPreviewNowAsync();
    }

    private async Task SearchPreviewEntitiesAsync(string searchTerm)
    {
        previewEntitySearchTerm = searchTerm;
        var type = formModel.Type;
        var entities = await operationRunner.RunAsync(
            (IForumPostTemplatePreviewEntityReadRepository repository) =>
                repository.SearchAsync(type, searchTerm, PreviewEntitySearchLimit)
        );

        if (formModel.Type != type || previewEntitySearchTerm != searchTerm)
        {
            return;
        }

        previewEntities = IncludeSelectedPreviewEntity(entities);
    }

    private IReadOnlyList<ForumPostTemplatePreviewEntityReadModel> IncludeSelectedPreviewEntity(
        IReadOnlyList<ForumPostTemplatePreviewEntityReadModel> entities
    )
    {
        if (previewEntity is null || entities.Contains(previewEntity))
        {
            return entities;
        }

        return [previewEntity, .. entities];
    }

    private void SchedulePreviewRender()
    {
        if (previewData is null)
        {
            previewRenderDelay.Cancel();
            return;
        }

        _ = RenderPreviewAfterDelayAsync();
    }

    private async Task RenderPreviewAfterDelayAsync()
    {
        if (!await previewRenderDelay.WaitAsync(PreviewRenderDelay))
        {
            return;
        }

        await InvokeAsync(async () =>
        {
            await RenderPreviewNowAsync();
            StateHasChanged();
        });
    }

    private async Task RenderPreviewNowAsync()
    {
        previewRenderDelay.Cancel();

        if (previewData is null)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var result = await ForumPostRenderService.RenderPreviewAsync(
            previewData,
            formModel.TemplateBody
        );
        previewRenderDuration = stopwatch.Elapsed;
        previewErrors = result.Errors;

        if (result.Errors.Count == 0)
        {
            previewContent = result.Content;
        }
    }

    private void JumpToFirstError()
    {
        var firstErrorWithPosition = validationResult.Errors.FirstOrDefault(error =>
            error.Line is not null
        );
        if (firstErrorWithPosition is not null)
        {
            JumpToError(firstErrorWithPosition);
        }
    }

    private void JumpToError(ForumPostTemplateError error)
    {
        mobilePane = EditorMobilePane;
        pendingEditorFocusError = error;
    }

    private void InsertVariable(ForumPostTemplateVariableInsertion insertion)
    {
        mobilePane = EditorMobilePane;
        pendingEditorInsertion = insertion;
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

    public async ValueTask DisposeAsync()
    {
        persistSubscription.Dispose();
        previewRenderDelay.Dispose();

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

        {{~ for upload in uploads ~}}
        [B]{{ upload.name }}[/B]
        {{~ for crypter in upload.link_crypters ~}}
        [URL='{{ crypter.container_link }}']{{ crypter.name }}[/URL]
        {{~ end ~}}

        {{~ end ~}}
        [/CENTER]
        """;

    private const string DefaultCollectionTemplate = """
        [CENTER]
        [B]{{ series.title }}[/B]

        [IMG]{{ series.cover_url }}[/IMG]

        {{ series.description }}

        {{~ for release in releases ~}}
        [B]{{ release.name }}[/B]
        {{~ for upload in release.uploads ~}}
        {{~ for crypter in upload.link_crypters ~}}
        [URL='{{ crypter.container_link }}']{{ upload.name }} - {{ crypter.name }}[/URL]
        {{~ end ~}}
        {{~ end ~}}

        {{~ end ~}}
        [/CENTER]
        """;
}
