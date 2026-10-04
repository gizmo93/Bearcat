using Bearcat.Abstractions.DistributionSite;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Domain.UseCases.ManageDistributionSites;
using Bearcat.Domain.UseCases.ManageDistributionSites.ReadModels;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Rendering;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Repositories;
using Bearcat.Domain.UseCases.ManagePostedLocations;
using Bearcat.Domain.UseCases.ManagePostedLocations.Repositories;
using Bearcat.Domain.UseCases.PostToForums;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.PostToForum;

public partial class PostToForumDialog(IScopedOperationRunner operationRunner) : ComponentBase
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

    private IReadOnlyList<DistributionSiteRegistrationReadModel> registrations = [];
    private int selectedRegistrationId;

    private IReadOnlyList<ForumTargetWithPath> targets = [];
    private string? selectedTargetId;

    private IReadOnlyList<ExistingThread> existingThreads = [];
    private string threadSelection = NewThreadValue;

    private IReadOnlyList<ForumPostTemplateSummaryReadModel> templates = [];
    private int selectedTemplateId;
    private string postName = string.Empty;
    private string body = string.Empty;
    private IReadOnlyList<string> renderErrors = [];

    private IReadOnlyList<ThreadPrefix> prefixes = [];
    private IReadOnlyList<string> selectedPrefixIds = [];

    private PreparedDraft? preparedDraft;

    private bool postRecorded;
    private string? savedPostUrl;
    private bool showManualUrlEntry;
    private string manualUrl = string.Empty;

    private bool IsNewThread => threadSelection == NewThreadValue;

    private bool IsCollection => TemplateType == ForumPostTemplateType.ReleaseCollection;

    private int? SelectedTemplateIdOrNull => selectedTemplateId == 0 ? null : selectedTemplateId;

    private IReadOnlyList<SelectOption<int>> RegistrationOptions =>
        registrations
            .Select(registration => new SelectOption<int>(
                registration.DistributionSiteRegistrationId,
                $"{registration.Name} ({registration.DistributionSiteName})"
            ))
            .ToList();

    private IReadOnlyList<SelectOption<string>> TargetOptions =>
        targets.Select(target => new SelectOption<string>(target.Id, target.Label)).ToList();

    private IReadOnlyList<SelectOption<string>> ThreadOptions =>
        [
            new(NewThreadValue, L["StartNewThread"]),
            .. existingThreads.Select(thread => new SelectOption<string>(thread.Url, thread.Title)),
        ];

    private IReadOnlyList<SelectOption<int>> TemplateOptions =>
        templates
            .Select(template => new SelectOption<int>(template.ForumPostTemplateId, template.Name))
            .ToList();

    private IReadOnlyList<SelectOption<string>> PrefixOptions =>
        prefixes.Select(prefix => new SelectOption<string>(prefix.Id, prefix.Label)).ToList();

    protected override async Task OnInitializedAsync()
    {
        postName = EntityName;

        var all = await operationRunner.RunAsync(
            (IDistributionSiteRegistrationReadRepository repository) => repository.GetAllAsync()
        );
        var forums = all.Where(registration =>
                registration.Kind == DistributionSiteKind.Forum && registration.IsActive
            )
            .ToList();

        registrations = await OrderByPostingHistoryAsync(forums);

        selectedRegistrationId =
            registrations.FirstOrDefault()?.DistributionSiteRegistrationId ?? 0;
    }

    private async Task<
        IReadOnlyList<DistributionSiteRegistrationReadModel>
    > OrderByPostingHistoryAsync(List<DistributionSiteRegistrationReadModel> forums)
    {
        var postedHosts = await GetPostedHostsAsync();
        var alreadyPostedRegistrationIds = await GetAlreadyPostedRegistrationIdsAsync(
            registrationIds: forums
                .Select(registration => registration.DistributionSiteRegistrationId)
                .ToList(),
            postedHosts: postedHosts
        );

        return forums
            .OrderBy(registration =>
                alreadyPostedRegistrationIds.Contains(registration.DistributionSiteRegistrationId)
                    ? 1
                    : 0
            )
            .ThenBy(registration => registration.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<HashSet<int>> GetAlreadyPostedRegistrationIdsAsync(
        List<int> registrationIds,
        HashSet<string> postedHosts
    )
    {
        var baseUrlsByRegistrationId = await operationRunner.RunAsync(
            (DistributionSiteRegistrationService service) =>
                service.GetBaseUrlsByRegistrationIdAsync(registrationIds)
        );

        return baseUrlsByRegistrationId
            .Where(entry => HasAlreadyPosted(entry.Value, postedHosts))
            .Select(entry => entry.Key)
            .ToHashSet();
    }

    private async Task<HashSet<string>> GetPostedHostsAsync()
    {
        var locations = await operationRunner.RunAsync(
            (IPostedLocationReadRepository repository) =>
                IsCollection
                    ? repository.GetForCollectionAsync(EntityId)
                    : repository.GetForReleaseAsync(EntityId)
        );

        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var location in locations)
        {
            if (Uri.TryCreate(location.Url, UriKind.Absolute, out var uri))
            {
                hosts.Add(RemoveWwwPrefix(uri.Host));
            }
        }

        return hosts;
    }

    private static bool HasAlreadyPosted(string baseUrl, HashSet<string> postedHosts)
    {
        return Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            && postedHosts.Contains(RemoveWwwPrefix(uri.Host));
    }

    private static string RemoveWwwPrefix(string host) =>
        host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;

    private async Task LoadHierarchyAsync()
    {
        await RunBusyAsync(async () =>
        {
            var hierarchy = await operationRunner.RunAsync(
                (DistributionSiteSessionService service) =>
                    service.GetTargetHierarchyAsync(selectedRegistrationId)
            );
            var targetsWithPath = new List<ForumTargetWithPath>();
            AddTargetsWithAncestorPath(hierarchy, ancestors: [], targetsWithPath);
            targets = targetsWithPath;
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
            existingThreads = await operationRunner.RunAsync(
                (DistributionSiteSessionService service) =>
                    service.FindExistingThreadsAsync(
                        registrationId: selectedRegistrationId,
                        target: new ForumTargetId(selectedTargetId),
                        releaseName: postName
                    )
            );
            threadSelection = existingThreads.FirstOrDefault()?.Url ?? NewThreadValue;
            step = WizardStep.Thread;
        });
    }

    private async Task PrepareComposeAsync()
    {
        await RunBusyAsync(async () =>
        {
            templates = await operationRunner.RunAsync(
                (IForumPostTemplateReadRepository repository) =>
                    repository.GetAllAsync(
                        TemplateType,
                        outputFormat: ForumPostTemplateOutputFormat.BBCode
                    )
            );
            selectedTemplateId = templates.FirstOrDefault()?.ForumPostTemplateId ?? 0;

            if (IsNewThread)
            {
                prefixes = await operationRunner.RunAsync(
                    (DistributionSiteSessionService service) =>
                        service.GetThreadPrefixesAsync(
                            registrationId: selectedRegistrationId,
                            target: new ForumTargetId(selectedTargetId!)
                        )
                );
                selectedPrefixIds = [];
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
            var result = await operationRunner.RunAsync(
                (ForumPostRenderService service) =>
                    service.RenderAsync(EntityId, selectedTemplateId)
            );
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
            preparedDraft = await operationRunner.RunAsync<
                DistributionSiteSessionService,
                PreparedDraft
            >(service =>
                IsNewThread
                    ? service.PrepareNewThreadDraftAsync(
                        registrationId: selectedRegistrationId,
                        target: new ForumTargetId(selectedTargetId!),
                        title: postName,
                        prefixIds: selectedPrefixIds,
                        body: body
                    )
                    : service.PrepareReplyDraftAsync(
                        registrationId: selectedRegistrationId,
                        threadUrl: threadSelection,
                        body: body
                    )
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
            var url = await operationRunner.RunAsync(
                (DistributionSiteSessionService service) =>
                    service.FindUrlOfSubmittedPostAsync(
                        registrationId: selectedRegistrationId,
                        target: new ForumTargetId(selectedTargetId ?? string.Empty),
                        isNewThread: IsNewThread,
                        threadUrl: IsNewThread ? string.Empty : threadSelection,
                        title: postName
                    )
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
        await operationRunner.RunAsync<PostedLocationService>(async service =>
        {
            if (IsCollection)
            {
                await service.AddForCollectionAsync(
                    releaseCollectionId: EntityId,
                    url: url,
                    distributionSiteRegistrationId: selectedRegistrationId,
                    forumPostTemplateId: SelectedTemplateIdOrNull
                );
                return;
            }

            await service.AddForReleaseAsync(
                releaseId: EntityId,
                url: url,
                distributionSiteRegistrationId: selectedRegistrationId,
                forumPostTemplateId: SelectedTemplateIdOrNull
            );
        });

        savedPostUrl = url.Trim();
        postRecorded = true;
        showManualUrlEntry = false;
    }

    private void StartManualUrlEntry()
    {
        manualUrl = IsNewThread ? preparedDraft?.DraftEditorUrl ?? string.Empty : threadSelection;
        showManualUrlEntry = true;
    }

    private void NormalizeReleaseName()
    {
        postName = ReleaseNameFormatter.ToSpacedName(postName);
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

    private static void AddTargetsWithAncestorPath(
        IReadOnlyList<ForumTargetNode> nodes,
        IReadOnlyList<string> ancestors,
        List<ForumTargetWithPath> targetsWithPath
    )
    {
        foreach (var node in nodes)
        {
            var path = ancestors.Append(node.Title).ToList();

            if (node.CanReceivePosts)
            {
                targetsWithPath.Add(
                    new ForumTargetWithPath(node.Id.Value, string.Join(" › ", path))
                );
            }

            AddTargetsWithAncestorPath(node.Children, path, targetsWithPath);
        }
    }

    private sealed record ForumTargetWithPath(string Id, string Label);
}
