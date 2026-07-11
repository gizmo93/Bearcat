using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Website.Formatting;
using Bearcat.Website.ScopedOperations;
using Microsoft.AspNetCore.Components;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Website.Pages.PostQueue;

public partial class PostQueuePage(
    IScopedOperationRunner operationRunner,
    PostQueueWorkflowState workflowState,
    NavigationManager navigationManager,
    TimeProvider timeProvider
) : ComponentBase
{
    private IReadOnlyList<ReleasePostQueueItemReadModel> releaseItems = [];
    private IReadOnlyList<CollectionPostQueueItemReadModel> collectionItems = [];
    private bool isLoading = true;
    private bool enabled = true;

    protected override async Task OnInitializedAsync()
    {
        enabled = operationRunner.Run(
            (IApplicationConfigurationProvider configuration) =>
                configuration.GetValue<PostQueueConfiguration>(c => c.Enabled)
        );

        if (!enabled)
        {
            isLoading = false;
            return;
        }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        isLoading = true;

        try
        {
            await operationRunner.RunAsync(
                async (
                    IReleaseReadRepository releaseRepository,
                    IReleaseCollectionReadRepository collectionRepository
                ) =>
                {
                    releaseItems = await releaseRepository.GetPostQueueAsync();
                    collectionItems = await collectionRepository.GetPostQueueAsync();
                }
            );
        }
        finally
        {
            isLoading = false;
        }
    }

    private void StartReleaseWorkflow(int? startReleaseId = null)
    {
        if (releaseItems.Count == 0)
        {
            return;
        }

        var ids = releaseItems.Select(item => item.ReleaseId).ToList();
        workflowState.Start(PostQueueWorkflowType.Release, OrderFrom(ids, startReleaseId));

        navigationManager.NavigateTo($"/releases/{startReleaseId ?? ids[0]}?workflow=postqueue");
    }

    private void StartCollectionWorkflow(int? startCollectionId = null)
    {
        if (collectionItems.Count == 0)
        {
            return;
        }

        var ids = collectionItems.Select(item => item.ReleaseCollectionId).ToList();
        workflowState.Start(PostQueueWorkflowType.Collection, OrderFrom(ids, startCollectionId));

        navigationManager.NavigateTo(
            $"/release-collections/{startCollectionId ?? ids[0]}?workflow=postqueue"
        );
    }

    private async Task MarkReleasePostedAsync(int releaseId)
    {
        await operationRunner.RunAsync(
            (ReleaseService service) => service.MarkUploadsPostedAsync(releaseId)
        );

        await LoadAsync();
    }

    private async Task MarkCollectionPostedAsync(int releaseCollectionId)
    {
        await operationRunner.RunAsync(
            (ReleaseCollectionService service) =>
                service.MarkUploadsPostedAsync(releaseCollectionId)
        );

        await LoadAsync();
    }

    private string HumanizeUploadedAt(DateTime uploadedAt) => timeProvider.Humanize(uploadedAt);

    private static IReadOnlyList<int> OrderFrom(IReadOnlyList<int> ids, int? startId)
    {
        if (startId is null)
        {
            return ids;
        }

        var index = ids.ToList().IndexOf(startId.Value);

        return index <= 0 ? ids : ids.Skip(index).Concat(ids.Take(index)).ToList();
    }
}
