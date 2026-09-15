using Bearcat.Abstractions.DistributionSite;
using Bearcat.Domain.UseCases.ManageDistributionSites.ReadModels;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;
using Bearcat.Domain.UseCases.ManageForumPostingRules;
using Bearcat.Domain.UseCases.ManageForumPostingRules.ReadModels;
using Bearcat.Domain.UseCases.ManageForumPostingRules.Repositories;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageForumPostingRules;

public partial class ForumPostingRulesPage(
    DialogService dialogService,
    ToastService toastService,
    NavigationManager navigationManager,
    IScopedOperationRunner operationRunner
) : ComponentBase
{
    [Parameter]
    public int DistributionSiteRegistrationId { get; set; }

    private const string RuleGridClass =
        "lg:grid-cols-[4.5rem_minmax(0,1fr)_minmax(0,1.3fr)_minmax(0,0.9fr)_11rem_5rem_4rem]";

    private DistributionSiteRegistrationReadModel registration = null!;
    private List<ForumPostingRuleSummaryReadModel> rules = [];
    private IReadOnlyList<ForumPostingRulePreviewReadModel>? previewResults;
    private bool isInitialized;
    private bool isPreviewing;

    private int MatchedCount =>
        previewResults?.Count(result => result.MatchedRuleName is not null) ?? 0;

    protected override async Task OnInitializedAsync()
    {
        var detail = await operationRunner.RunAsync(
            (IDistributionSiteRegistrationReadRepository repository) =>
                repository.GetByIdAsync(DistributionSiteRegistrationId)
        );

        if (detail is null || detail.Kind != DistributionSiteKind.Forum)
        {
            navigationManager.NotFound();

            return;
        }

        registration = detail;
        await LoadRulesAsync();
        isInitialized = true;
    }

    private async Task LoadRulesAsync()
    {
        var summaries = await operationRunner.RunAsync(
            (IForumPostingRuleReadRepository repository) =>
                repository.GetAllAsync(DistributionSiteRegistrationId)
        );

        rules = [.. summaries];
    }

    private async Task ShowAddDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditForumPostingRuleDialog.DistributionSiteRegistrationId)] =
                DistributionSiteRegistrationId,
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditForumPostingRuleDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["NewForumPostingRule"],
                Description = L["ForumPostingRuleDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadRulesAsync();
        }
    }

    private async Task ShowEditDialogAsync(ForumPostingRuleSummaryReadModel rule)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditForumPostingRuleDialog.DistributionSiteRegistrationId)] =
                DistributionSiteRegistrationId,
            [nameof(CreateOrEditForumPostingRuleDialog.ForumPostingRuleId)] =
                rule.ForumPostingRuleId,
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditForumPostingRuleDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditNamedItem", rule.Name],
                Description = L["ForumPostingRuleDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadRulesAsync();
        }
    }

    private async Task DeleteAsync(ForumPostingRuleSummaryReadModel rule)
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", rule.Name],
            L["DeleteForumPostingRuleConfirmation", rule.Name],
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
            (ForumPostingRuleService service) => service.DeleteAsync(rule.ForumPostingRuleId)
        );
        await LoadRulesAsync();
        previewResults = null;
    }

    private async Task ToggleIsEnabledAsync(ForumPostingRuleSummaryReadModel rule, bool isEnabled)
    {
        await operationRunner.RunAsync(
            (ForumPostingRuleService service) =>
                service.ToggleIsEnabledAsync(rule.ForumPostingRuleId, isEnabled)
        );

        toastService.Success(
            isEnabled
                ? L["ForumPostingRuleEnabled", rule.Name]
                : L["ForumPostingRuleDisabled", rule.Name]
        );
        await LoadRulesAsync();
        previewResults = null;
    }

    private async Task ReorderByDragAsync((int OldIndex, int NewIndex) move)
    {
        if (
            move.OldIndex == move.NewIndex
            || move.OldIndex < 0
            || move.OldIndex >= rules.Count
            || move.NewIndex < 0
            || move.NewIndex >= rules.Count
        )
        {
            return;
        }

        var rule = rules[move.OldIndex];
        rules.RemoveAt(move.OldIndex);
        rules.Insert(move.NewIndex, rule);

        var ordered = rules.Select(candidate => candidate.ForumPostingRuleId).ToList();

        await operationRunner.RunAsync(
            (ForumPostingRuleService service) =>
                service.ReorderAsync(DistributionSiteRegistrationId, ordered)
        );
        await LoadRulesAsync();
        previewResults = null;
    }

    private async Task RunPreviewAsync()
    {
        isPreviewing = true;

        try
        {
            previewResults = await operationRunner.RunAsync(
                (ForumPostingRulePreviewService service) =>
                    service.PreviewAsync(DistributionSiteRegistrationId)
            );
        }
        finally
        {
            isPreviewing = false;
        }
    }
}
