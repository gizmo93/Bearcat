using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Humanizer;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Website.Pages.PostQueue;

public partial class PostQueuePage(
    PostQueueWorkflowState workflowState,
    NavigationManager navigationManager,
    TimeProvider timeProvider
) : OwningComponentBase
{
    private IReleaseReadRepository releaseReadRepository = null!;
    private IReleaseCollectionReadRepository collectionReadRepository = null!;

    private IReadOnlyList<ReleasePostQueueItemReadModel> releaseItems = [];
    private IReadOnlyList<CollectionPostQueueItemReadModel> collectionItems = [];
    private bool isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        releaseReadRepository = ScopedServices.GetRequiredService<IReleaseReadRepository>();
        collectionReadRepository =
            ScopedServices.GetRequiredService<IReleaseCollectionReadRepository>();

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        isLoading = true;

        try
        {
            releaseItems = await releaseReadRepository.GetPostQueueAsync();
            collectionItems = await collectionReadRepository.GetPostQueueAsync();
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
        await ScopedServices.GetRequiredService<ReleaseService>().MarkUploadsPostedAsync(releaseId);
        await LoadAsync();
    }

    private async Task MarkCollectionPostedAsync(int releaseCollectionId)
    {
        await ScopedServices
            .GetRequiredService<ReleaseCollectionService>()
            .MarkUploadsPostedAsync(releaseCollectionId);
        await LoadAsync();
    }

    private string HumanizeUploadedAt(DateTime uploadedAt) =>
        uploadedAt.Humanize(utcDate: false, dateToCompareAgainst: timeProvider.GetLocalNow());

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
