using Bearcat.Abstractions.DistributionSite;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Domain.UseCases.ManageDistributionSites;
using Bearcat.Domain.UseCases.ManageDistributionSites.ReadModels;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Rendering;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Repositories;
using Bearcat.Domain.UseCases.ManagePostedLocations;
using Bearcat.Domain.ValueObjects;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.PostToForum;

public partial class PostToForumDialog(
    IDistributionSiteRegistrationReadRepository registrationReadRepository,
    IForumPostTemplateReadRepository templateReadRepository,
    ForumPostRenderService renderService
) : OwningComponentBase
{
    [Parameter]
    public int EntityId { get; set; }

    [Parameter]
    public string EntityName { get; set; } = string.Empty;

    [Parameter]
    public ForumPostTemplateType TemplateType { get; set; } = ForumPostTemplateType.Release;

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private const string NewThreadValue = "new";

    private enum WizardStep
    {
        Site,
        Target,
        Thread,
        Compose,
        Done,
    }

    private WizardStep step = WizardStep.Site;
    private bool isBusy;
    private string? errorMessage;

    private DistributionSiteSessionService sessionService = null!;

    private IReadOnlyList<DistributionSiteRegistrationReadModel> registrations = [];
    private int selectedRegistrationId;

    private IReadOnlyList<FlatForumTarget> targets = [];
    private string? selectedTargetId;

    private IReadOnlyList<ExistingThread> existingThreads = [];
    private string threadSelection = NewThreadValue;

    private IReadOnlyList<ForumPostTemplateSummaryReadModel> templates = [];
    private int selectedTemplateId;
    private string postName = string.Empty;
    private string body = string.Empty;
    private IReadOnlyList<string> renderErrors = [];

    private IReadOnlyList<ThreadPrefix> prefixes = [];
    private IEnumerable<string> selectedPrefixIds = new List<string>();

    private PreparedDraft? preparedDraft;

    private bool postRecorded;
    private string? savedPostUrl;
    private bool showManualUrlEntry;
    private string manualUrl = string.Empty;

    private bool IsNewThread => threadSelection == NewThreadValue;

    private bool IsCollection => TemplateType == ForumPostTemplateType.ReleaseCollection;

    private IEnumerable<SelectOption<int>> RegistrationOptions =>
        registrations.Select(registration => new SelectOption<int>(
            registration.DistributionSiteRegistrationId,
            $"{registration.Name} ({registration.DistributionSiteName})"
        ));

    private IEnumerable<SelectOption<string>> TargetOptions =>
        targets.Select(target => new SelectOption<string>(target.Id, target.Label));

    private IEnumerable<SelectOption<string>> ThreadOptions =>
        new[] { new SelectOption<string>(NewThreadValue, L["StartNewThread"]) }.Concat(
            existingThreads.Select(thread => new SelectOption<string>(thread.Url, thread.Title))
        );

    private IEnumerable<SelectOption<int>> TemplateOptions =>
        templates.Select(template => new SelectOption<int>(
            template.ForumPostTemplateId,
            template.Name
        ));

    private IEnumerable<SelectOption<string>> PrefixOptions =>
        prefixes.Select(prefix => new SelectOption<string>(prefix.Id, prefix.Label));

    protected override async Task OnInitializedAsync()
    {
        sessionService = ScopedServices.GetRequiredService<DistributionSiteSessionService>();
        postName = EntityName;

        var all = await registrationReadRepository.GetAllAsync();
        registrations = all.Where(registration =>
                registration.Kind == DistributionSiteKind.Forum && registration.IsActive
            )
            .ToList();

        selectedRegistrationId =
            registrations.FirstOrDefault()?.DistributionSiteRegistrationId ?? 0;
    }

    private async Task LoadHierarchyAsync()
    {
        await RunBusyAsync(async () =>
        {
            var hierarchy = await sessionService.GetTargetHierarchyAsync(selectedRegistrationId);
            var flattened = new List<FlatForumTarget>();
            Flatten(hierarchy, depth: 0, flattened);
            targets = flattened;
            selectedTargetId = targets.FirstOrDefault()?.Id;
            step = WizardStep.Target;
        });
    }

    private async Task SearchThreadsAsync()
    {
        if (selectedTargetId is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(postName))
        {
            errorMessage = L["ReleaseNameRequired"];
            return;
        }

        await RunBusyAsync(async () =>
        {
            existingThreads = await sessionService.FindExistingThreadsAsync(
                registrationId: selectedRegistrationId,
                target: new ForumTargetId(selectedTargetId),
                releaseName: postName
            );
            threadSelection = existingThreads.FirstOrDefault()?.Url ?? NewThreadValue;
            step = WizardStep.Thread;
        });
    }

    private async Task PrepareComposeAsync()
    {
        await RunBusyAsync(async () =>
        {
            templates = await templateReadRepository.GetAllAsync(TemplateType);
            selectedTemplateId = templates.FirstOrDefault()?.ForumPostTemplateId ?? 0;

            if (IsNewThread)
            {
                prefixes = await sessionService.GetThreadPrefixesAsync(
                    registrationId: selectedRegistrationId,
                    target: new ForumTargetId(selectedTargetId!)
                );
                selectedPrefixIds = new List<string>();
            }
            else
            {
                prefixes = [];
            }

            step = WizardStep.Compose;

            if (selectedTemplateId != 0)
            {
                await RenderBodyAsync();
            }
        });
    }

    private async Task HandleTemplateChangedAsync(int value)
    {
        selectedTemplateId = value;
        await RenderBodyAsync();
    }

    private async Task RenderBodyAsync()
    {
        if (selectedTemplateId == 0)
        {
            return;
        }

        renderErrors = [];

        try
        {
            var result = await renderService.RenderAsync(EntityId, selectedTemplateId);
            body = result.Content;
            renderErrors = result.Errors;
        }
        catch (Exception exception)
        {
            renderErrors = [exception.Message];
        }
    }

    private async Task CreateDraftAsync()
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            errorMessage = L["ForumPostBodyRequired"];
            return;
        }

        if (IsNewThread && string.IsNullOrWhiteSpace(postName))
        {
            errorMessage = L["ThreadTitleRequired"];
            return;
        }

        await RunBusyAsync(async () =>
        {
            preparedDraft = IsNewThread
                ? await sessionService.PrepareNewThreadDraftAsync(
                    registrationId: selectedRegistrationId,
                    target: new ForumTargetId(selectedTargetId!),
                    title: postName,
                    prefixIds: selectedPrefixIds.ToList(),
                    body: body
                )
                : await sessionService.PrepareReplyDraftAsync(
                    registrationId: selectedRegistrationId,
                    threadUrl: threadSelection,
                    body: body
                );

            step = WizardStep.Done;
        });
    }

    private async Task ConfirmPostedAsync()
    {
        isBusy = true;
        errorMessage = null;

        try
        {
            var url = await sessionService.ResolvePostedUrlAsync(
                registrationId: selectedRegistrationId,
                target: new ForumTargetId(selectedTargetId ?? string.Empty),
                isNewThread: IsNewThread,
                threadUrl: IsNewThread ? string.Empty : threadSelection,
                title: postName
            );

            if (url is not null)
            {
                await SavePostedLocationAsync(url);
            }
            else
            {
                StartManualUrlEntry();
            }
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
            StartManualUrlEntry();
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task SaveManualUrlAsync()
    {
        if (string.IsNullOrWhiteSpace(manualUrl))
        {
            errorMessage = L["PostedLocationUrlRequired"];
            return;
        }

        await RunBusyAsync(() => SavePostedLocationAsync(manualUrl));
    }

    private async Task SavePostedLocationAsync(string url)
    {
        var service = ScopedServices.GetRequiredService<PostedLocationService>();

        if (IsCollection)
        {
            await service.AddForCollectionAsync(EntityId, url);
        }
        else
        {
            await service.AddForReleaseAsync(EntityId, url);
        }

        savedPostUrl = url.Trim();
        postRecorded = true;
        showManualUrlEntry = false;
    }

    private void StartManualUrlEntry()
    {
        manualUrl = IsNewThread ? preparedDraft?.OpenUrl ?? string.Empty : threadSelection;
        showManualUrlEntry = true;
    }

    private void NormalizeReleaseName()
    {
        var normalized = postName.Replace('.', ' ');

        // Add whitespaces between the hyphens where the release group is => <release name> - <release group>
        var lastHyphen = normalized.LastIndexOf('-');
        if (lastHyphen > 0 && lastHyphen < normalized.Length - 1)
        {
            var beforeGroup = normalized[..lastHyphen].TrimEnd();
            var group = normalized[(lastHyphen + 1)..].TrimStart();
            normalized = $"{beforeGroup} - {group}";
        }

        postName = normalized;
    }

    private void GoBack()
    {
        errorMessage = null;
        step = step switch
        {
            WizardStep.Target => WizardStep.Site,
            WizardStep.Thread => WizardStep.Target,
            WizardStep.Compose => WizardStep.Thread,
            _ => step,
        };
    }

    private async Task CloseAsync()
    {
        await DialogRef.CancelAsync();
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        isBusy = true;
        errorMessage = null;

        try
        {
            await action();
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
        }
        finally
        {
            isBusy = false;
        }
    }

    private static void Flatten(
        IReadOnlyList<ForumTargetNode> nodes,
        int depth,
        List<FlatForumTarget> accumulator
    )
    {
        foreach (var node in nodes)
        {
            if (node.CanReceivePosts)
            {
                var indent = string.Concat(Enumerable.Repeat("— ", depth));
                accumulator.Add(new FlatForumTarget(node.Id.Value, indent + node.Title));
            }

            Flatten(node.Children, depth + 1, accumulator);
        }
    }

    private sealed record FlatForumTarget(string Id, string Label);
}
