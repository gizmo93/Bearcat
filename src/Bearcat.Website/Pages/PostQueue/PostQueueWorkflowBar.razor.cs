using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleases;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.PostQueue;

public partial class PostQueueWorkflowBar(
    PostQueueWorkflowState workflowState,
    NavigationManager navigationManager
) : OwningComponentBase
{
    [Parameter]
    public PostQueueWorkflowType Type { get; set; }

    [Parameter]
    public int CurrentId { get; set; }

    private bool isBusy;

    private PostQueueWorkflowRun? Run => workflowState.GetRun(Type);

    private int ProgressPercent =>
        Run is { Total: > 0 } run ? (int)Math.Round(run.CompletedCount * 100.0 / run.Total) : 0;

    private async Task CompleteAndContinueAsync()
    {
        if (isBusy)
        {
            return;
        }

        isBusy = true;

        try
        {
            await MarkPostedAsync(CurrentId);
            var nextId = Run?.Complete(CurrentId);
            NavigateNext(nextId);
        }
        finally
        {
            isBusy = false;
        }
    }

    private void Skip()
    {
        var nextId = Run?.Skip(CurrentId);
        NavigateNext(nextId);
    }

    private void Leave()
    {
        workflowState.Clear(Type);
        navigationManager.NavigateTo("/post-queue");
    }

    private async Task MarkPostedAsync(int id)
    {
        switch (Type)
        {
            case PostQueueWorkflowType.Release:
                await ScopedServices
                    .GetRequiredService<ReleaseService>()
                    .MarkUploadsPostedAsync(id);
                break;
            case PostQueueWorkflowType.Collection:
                await ScopedServices
                    .GetRequiredService<ReleaseCollectionService>()
                    .MarkUploadsPostedAsync(id);
                break;
        }
    }

    private void NavigateNext(int? nextId)
    {
        if (nextId is null)
        {
            workflowState.Clear(Type);
            navigationManager.NavigateTo("/post-queue");
            return;
        }

        navigationManager.NavigateTo(BuildDetailUrl(nextId.Value));
    }

    private string BuildDetailUrl(int id) =>
        Type switch
        {
            PostQueueWorkflowType.Release => $"/releases/{id}?workflow=postqueue",
            PostQueueWorkflowType.Collection => $"/release-collections/{id}?workflow=postqueue",
            _ => "/post-queue",
        };
}
