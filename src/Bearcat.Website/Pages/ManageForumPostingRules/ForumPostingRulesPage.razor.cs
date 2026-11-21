using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.ImageHoster;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.Shared.ForumPosting;
using Bearcat.Domain.UseCases.ManageDistributionSites;
using Bearcat.Domain.UseCases.ManageDistributionSites.ReadModels;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;
using Bearcat.Domain.UseCases.ManageForumPostingRules;
using Bearcat.Domain.UseCases.ManageForumPostingRules.ReadModels;
using Bearcat.Domain.UseCases.ManageForumPostingRules.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
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

    private DistributionSiteServiceRestrictions serviceRestrictions =
        DistributionSiteServiceRestrictions.Empty;

    private IReadOnlyList<SelectOption<string>> hosterOptions = [];
    private IReadOnlyList<SelectOption<string>> linkCrypterOptions = [];
    private IReadOnlyList<SelectOption<string>> imageHosterOptions = [];
    private List<ForumPostingRuleSummaryReadModel> rules = [];
    private IReadOnlyList<ForumPostingRulePreviewReadModel>? previewResults;
    private ForumPostTemplateType previewEntityType = ForumPostTemplateType.Release;
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

        if (detail is null)
        {
            navigationManager.NotFound();

            return;
        }

        registration = detail;

        serviceRestrictions = await operationRunner.RunAsync(
            (IDistributionSiteRegistrationReadRepository repository) =>
                repository.GetServiceRestrictionsAsync(DistributionSiteRegistrationId)
        );

        LoadServiceOptions();
        await LoadRulesAsync();
        isInitialized = true;
    }

    private void LoadServiceOptions()
    {
        hosterOptions = operationRunner
            .Run((IHosterFactory factory) => factory.GetHosterReadModels())
            .Select(hoster => new SelectOption<string>(hoster.HosterClassName, hoster.Name))
            .ToList();

        linkCrypterOptions = operationRunner
            .Run((ILinkCrypterFactory factory) => factory.GetLinkCrypters())
            .Select(linkCrypter => new SelectOption<string>(
                linkCrypter.ClassName,
                linkCrypter.Name
            ))
            .ToList();

        imageHosterOptions = operationRunner
            .Run((IImageHosterFactory factory) => factory.GetImageHosters())
            .Select(imageHoster => new SelectOption<string>(
                imageHoster.ClassName,
                imageHoster.Name
            ))
            .ToList();
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
            [nameof(CreateOrEditForumPostingRuleDialog.TemplateOutputFormat)] =
                registration.TemplateOutputFormat,
            [nameof(CreateOrEditForumPostingRuleDialog.RegistrationKind)] = registration.Kind,
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
            [nameof(CreateOrEditForumPostingRuleDialog.TemplateOutputFormat)] =
                registration.TemplateOutputFormat,
            [nameof(CreateOrEditForumPostingRuleDialog.RegistrationKind)] = registration.Kind,
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

    private async Task SetAutomaticPostingAsync(bool isEnabled)
    {
        await operationRunner.RunAsync(
            (DistributionSiteRegistrationService service) =>
                service.SetAutomaticPostingAsync(DistributionSiteRegistrationId, isEnabled)
        );

        registration = registration with { EnableAutomaticPosting = isEnabled };

        toastService.Success(
            isEnabled
                ? L["AutomaticPostingEnabled", registration.Name]
                : L["AutomaticPostingDisabled", registration.Name]
        );
    }

    private async Task SetStripDotsForThreadSearchAsync(bool isEnabled)
    {
        await operationRunner.RunAsync(
            (DistributionSiteRegistrationService service) =>
                service.SetStripDotsForThreadSearchAsync(DistributionSiteRegistrationId, isEnabled)
        );

        registration = registration with { StripDotsForThreadSearch = isEnabled };

        toastService.Success(
            isEnabled
                ? L["StripDotsForThreadSearchEnabled", registration.Name]
                : L["StripDotsForThreadSearchDisabled", registration.Name]
        );
    }

    private async Task SetMinimumSecondsBetweenPostsAsync(int minimumSecondsBetweenPosts)
    {
        await operationRunner.RunAsync(
            (DistributionSiteRegistrationService service) =>
                service.SetMinimumSecondsBetweenPostsAsync(
                    DistributionSiteRegistrationId,
                    minimumSecondsBetweenPosts
                )
        );

        registration = registration with
        {
            MinimumSecondsBetweenPosts = minimumSecondsBetweenPosts,
        };

        toastService.Success(L["MinimumSecondsBetweenPostsSaved", registration.Name]);
    }

    private Task SetRequiredHosterClassNamesAsync(IEnumerable<string>? values)
    {
        var classNames = values?.ToList() ?? [];

        return SetServiceRestrictionsAsync(
            serviceRestrictions with
            {
                RequiredHosterClassNames = classNames,
                ForbiddenHosterClassNames = serviceRestrictions
                    .ForbiddenHosterClassNames.Except(classNames)
                    .ToList(),
            }
        );
    }

    private Task SetForbiddenHosterClassNamesAsync(IEnumerable<string>? values)
    {
        var classNames = values?.ToList() ?? [];

        return SetServiceRestrictionsAsync(
            serviceRestrictions with
            {
                ForbiddenHosterClassNames = classNames,
                RequiredHosterClassNames = serviceRestrictions
                    .RequiredHosterClassNames.Except(classNames)
                    .ToList(),
            }
        );
    }

    private Task SetRequiredLinkCrypterClassNamesAsync(IEnumerable<string>? values)
    {
        var classNames = values?.ToList() ?? [];

        return SetServiceRestrictionsAsync(
            serviceRestrictions with
            {
                RequiredLinkCrypterClassNames = classNames,
                ForbiddenLinkCrypterClassNames = serviceRestrictions
                    .ForbiddenLinkCrypterClassNames.Except(classNames)
                    .ToList(),
            }
        );
    }

    private Task SetForbiddenLinkCrypterClassNamesAsync(IEnumerable<string>? values)
    {
        var classNames = values?.ToList() ?? [];

        return SetServiceRestrictionsAsync(
            serviceRestrictions with
            {
                ForbiddenLinkCrypterClassNames = classNames,
                RequiredLinkCrypterClassNames = serviceRestrictions
                    .RequiredLinkCrypterClassNames.Except(classNames)
                    .ToList(),
            }
        );
    }

    private Task SetRequiredImageHosterClassNamesAsync(IEnumerable<string>? values)
    {
        var classNames = values?.ToList() ?? [];

        return SetServiceRestrictionsAsync(
            serviceRestrictions with
            {
                RequiredImageHosterClassNames = classNames,
                ForbiddenImageHosterClassNames = serviceRestrictions
                    .ForbiddenImageHosterClassNames.Except(classNames)
                    .ToList(),
            }
        );
    }

    private Task SetForbiddenImageHosterClassNamesAsync(IEnumerable<string>? values)
    {
        var classNames = values?.ToList() ?? [];

        return SetServiceRestrictionsAsync(
            serviceRestrictions with
            {
                ForbiddenImageHosterClassNames = classNames,
                RequiredImageHosterClassNames = serviceRestrictions
                    .RequiredImageHosterClassNames.Except(classNames)
                    .ToList(),
            }
        );
    }

    private async Task SetServiceRestrictionsAsync(DistributionSiteServiceRestrictions restrictions)
    {
        await operationRunner.RunAsync(
            (DistributionSiteRegistrationService service) =>
                service.SetServiceRestrictionsAsync(DistributionSiteRegistrationId, restrictions)
        );

        serviceRestrictions = restrictions;
        toastService.Success(L["ForumServiceRestrictionsSaved", registration.Name]);
    }

    private async Task ReorderByDragAsync((int OldIndex, int NewIndex) move)
    {
        if (!SortableListReordering.TryMoveItem(rules, move))
        {
            return;
        }

        var ordered = rules.Select(candidate => candidate.ForumPostingRuleId).ToList();

        await operationRunner.RunAsync(
            (ForumPostingRuleService service) =>
                service.ReorderAsync(DistributionSiteRegistrationId, ordered)
        );
        await LoadRulesAsync();
        previewResults = null;
    }

    private void ChangePreviewEntityType(ForumPostTemplateType entityType)
    {
        previewEntityType = entityType;
        previewResults = null;
    }

    private async Task RunPreviewAsync()
    {
        isPreviewing = true;

        try
        {
            previewResults = await operationRunner.RunAsync(
                (ForumPostingRulePreviewService service) =>
                    service.PreviewAsync(DistributionSiteRegistrationId, previewEntityType)
            );
        }
        finally
        {
            isPreviewing = false;
        }
    }
}
