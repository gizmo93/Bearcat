using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Domain.UseCases.ManageDistributionSites;
using Bearcat.Domain.UseCases.ManageForumPostingRules;
using Bearcat.Domain.UseCases.ManageForumPostingRules.Dto;
using Bearcat.Domain.UseCases.ManageForumPostingRules.Repositories;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Bearcat.Website.Pages.ManageForumPostingRules;

public partial class CreateOrEditForumPostingRuleDialog(IScopedOperationRunner operationRunner)
    : ComponentBase
{
    [Parameter]
    public int DistributionSiteRegistrationId { get; set; }

    [Parameter]
    public int? ForumPostingRuleId { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private const string NoThreadPrefixValue = "";

    private ForumPostingRuleFormModel formModel = new();
    private ConditionNodeModel conditionRoot = ConditionModelMapper.CreateDefaultRoot();
    private EditContext editContext = null!;
    private ValidationMessageStore validationMessageStore = null!;
    private bool isInitialized;
    private bool isSaving;
    private string? errorMessage;

    private IReadOnlyList<ForumPostTemplateSummaryReadModel> templates = [];

    private List<FlatForumTarget> targets = [];
    private bool targetsLoaded;
    private bool isLoadingTargets;
    private string? targetsErrorMessage;

    private IReadOnlyList<ThreadPrefix> prefixes = [];
    private bool prefixesLoaded;
    private bool isLoadingPrefixes;
    private string? prefixesErrorMessage;

    private string? SelectedTargetUrl =>
        targets.FirstOrDefault(target => target.Key == formModel.TargetNodeId)?.Url;

    private IReadOnlyList<SelectOption<int>> TemplateOptions =>
        templates
            .Select(template => new SelectOption<int>(template.ForumPostTemplateId, template.Name))
            .ToList();

    private IReadOnlyList<SelectOption<ForumPostPostMode>> PostModeOptions =>
        Enum.GetValues<ForumPostPostMode>()
            .Select(mode => new SelectOption<ForumPostPostMode>(mode, L.Localize(mode)))
            .ToList();

    private IReadOnlyList<SelectOption<string>> PrefixOptions =>
        [
            new(NoThreadPrefixValue, L["NoThreadPrefix"]),
            .. prefixes.Select(prefix => new SelectOption<string>(prefix.Id, prefix.Label)),
        ];

    private IReadOnlyList<SelectOption<string>> TargetOptions
    {
        get
        {
            var options = targets
                .Select(target => new SelectOption<string>(target.Key, target.Label))
                .ToList();

            if (
                !string.IsNullOrWhiteSpace(formModel.TargetNodeId)
                && options.All(option => option.Value != formModel.TargetNodeId)
            )
            {
                options.Insert(
                    0,
                    new SelectOption<string>(
                        formModel.TargetNodeId,
                        string.IsNullOrWhiteSpace(formModel.TargetPathSnapshot)
                            ? formModel.TargetNodeId
                            : formModel.TargetPathSnapshot
                    )
                );
            }

            return options;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        templates = await operationRunner.RunAsync(
            (IForumPostTemplateReadRepository repository) =>
                repository.GetAllAsync(ForumPostTemplateType.Release)
        );

        await InitializeFormModelAsync();

        editContext = new EditContext(formModel);
        editContext.OnValidationRequested += OnValidationRequested;
        validationMessageStore = new ValidationMessageStore(editContext);
        isInitialized = true;
    }

    private async Task InitializeFormModelAsync()
    {
        if (ForumPostingRuleId is null)
        {
            formModel = new ForumPostingRuleFormModel
            {
                ForumPostTemplateId = templates.FirstOrDefault()?.ForumPostTemplateId ?? 0,
            };
            conditionRoot = ConditionModelMapper.CreateDefaultRoot();

            return;
        }

        var detail = await operationRunner.RunAsync(
            (IForumPostingRuleReadRepository repository) =>
                repository.GetDetailAsync(ForumPostingRuleId.Value)
        );

        if (detail is null)
        {
            await DialogRef.CancelAsync();

            return;
        }

        formModel = new ForumPostingRuleFormModel
        {
            ForumPostingRuleId = detail.ForumPostingRuleId,
            Name = detail.Name,
            TargetNodeId = detail.TargetNodeId,
            TargetPathSnapshot = detail.TargetPathSnapshot,
            ThreadPrefixId = detail.ThreadPrefixId ?? NoThreadPrefixValue,
            ForumPostTemplateId = detail.ForumPostTemplateId,
            PostMode = detail.PostMode,
            IsEnabled = detail.IsEnabled,
        };
        conditionRoot = ConditionModelMapper.FromJson(detail.ConditionJson);
    }

    private async Task LoadTargetsAsync()
    {
        isLoadingTargets = true;
        targetsErrorMessage = null;

        try
        {
            var hierarchy = await operationRunner.RunAsync(
                (DistributionSiteSessionService service) =>
                    service.GetTargetHierarchyAsync(DistributionSiteRegistrationId)
            );

            var flattened = new List<FlatForumTarget>();
            Flatten(hierarchy, ancestors: [], flattened);
            targets = flattened;
            targetsLoaded = true;
        }
        catch (Exception exception)
        {
            targetsErrorMessage = exception.Message;
        }
        finally
        {
            isLoadingTargets = false;
        }
    }

    private void SelectTarget(string? targetKey)
    {
        formModel.TargetNodeId = targetKey;

        var target = targets.FirstOrDefault(candidate => candidate.Key == targetKey);

        if (target is not null)
        {
            formModel.TargetPathSnapshot = target.Label;
        }

        prefixes = [];
        prefixesLoaded = false;
        prefixesErrorMessage = null;
        formModel.ThreadPrefixId = NoThreadPrefixValue;
    }

    private async Task LoadPrefixesAsync()
    {
        if (SelectedTargetUrl is null)
        {
            return;
        }

        isLoadingPrefixes = true;
        prefixesErrorMessage = null;

        try
        {
            prefixes = await operationRunner.RunAsync(
                (DistributionSiteSessionService service) =>
                    service.GetThreadPrefixesAsync(
                        registrationId: DistributionSiteRegistrationId,
                        target: new ForumTargetId(SelectedTargetUrl)
                    )
            );
            prefixesLoaded = true;
        }
        catch (Exception exception)
        {
            prefixesErrorMessage = exception.Message;
        }
        finally
        {
            isLoadingPrefixes = false;
        }
    }

    private async Task SaveAsync()
    {
        isSaving = true;
        errorMessage = null;

        var input = new ForumPostingRuleInput(
            Name: formModel.Name,
            ConditionJson: ConditionModelMapper.ToJson(conditionRoot),
            TargetNodeId: formModel.TargetNodeId ?? string.Empty,
            TargetPathSnapshot: formModel.TargetPathSnapshot,
            ThreadPrefixId: string.IsNullOrWhiteSpace(formModel.ThreadPrefixId)
                ? null
                : formModel.ThreadPrefixId,
            ForumPostTemplateId: formModel.ForumPostTemplateId,
            PostMode: formModel.PostMode,
            IsEnabled: formModel.IsEnabled
        );

        try
        {
            if (formModel.IsEdit)
            {
                await operationRunner.RunAsync(
                    (ForumPostingRuleService service) =>
                        service.UpdateAsync(formModel.ForumPostingRuleId!.Value, input)
                );
            }
            else
            {
                await operationRunner.RunAsync(
                    (ForumPostingRuleService service) =>
                        service.CreateAsync(DistributionSiteRegistrationId, input)
                );
            }
        }
        catch (ArgumentException exception)
        {
            errorMessage = exception.Message;

            return;
        }
        finally
        {
            isSaving = false;
        }

        await DialogRef.CloseAsync(DialogResult.Ok());
    }

    private void OnValidationRequested(object? sender, ValidationRequestedEventArgs args)
    {
        validationMessageStore.Clear();

        if (string.IsNullOrWhiteSpace(formModel.Name))
        {
            validationMessageStore.Add(() => formModel.Name, L["NameIsRequired"]);
        }

        if (string.IsNullOrWhiteSpace(formModel.TargetNodeId))
        {
            validationMessageStore.Add(() => formModel.TargetNodeId!, L["SubforumIsRequired"]);
        }

        if (formModel.ForumPostTemplateId == 0)
        {
            validationMessageStore.Add(
                () => formModel.ForumPostTemplateId,
                L["ForumPostTemplateIsRequired"]
            );
        }
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }

    private static void Flatten(
        IReadOnlyList<ForumTargetNode> nodes,
        IReadOnlyList<string> ancestors,
        List<FlatForumTarget> accumulator
    )
    {
        foreach (var node in nodes)
        {
            var path = ancestors.Append(node.Title).ToList();

            if (node.CanReceivePosts)
            {
                accumulator.Add(
                    new FlatForumTarget(
                        Key: node.StableId ?? node.Id.Value,
                        Url: node.Id.Value,
                        Label: string.Join(" › ", path)
                    )
                );
            }

            Flatten(node.Children, path, accumulator);
        }
    }

    private sealed record FlatForumTarget(string Key, string Url, string Label);
}
