using Bearcat.Domain.UseCases.ManagePostedLocations;
using Bearcat.Domain.UseCases.ManagePostedLocations.ReadModels;
using Bearcat.Domain.UseCases.ManagePostedLocations.Repositories;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManagePostedLocations;

public partial class PostedLocations(IScopedOperationRunner operationRunner)
    : ComponentBase,
        IReloadableComponent
{
    [Parameter]
    public int? ReleaseId { get; set; }

    [Parameter]
    public int? ReleaseCollectionId { get; set; }

    private IReadOnlyList<PostedLocationReadModel> locations = [];
    private string newUrl = string.Empty;
    private bool isBusy;
    private string? errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        locations = await operationRunner.RunAsync(
            (IPostedLocationReadRepository repository) =>
                ReleaseCollectionId is { } collectionId
                    ? repository.GetForCollectionAsync(collectionId)
                    : repository.GetForReleaseAsync(ReleaseId!.Value)
        );

        StateHasChanged();
    }

    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(newUrl))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await operationRunner.RunAsync<PostedLocationService>(async service =>
            {
                if (ReleaseCollectionId is { } collectionId)
                {
                    await service.AddForCollectionAsync(collectionId, newUrl);
                    return;
                }

                await service.AddForReleaseAsync(ReleaseId!.Value, newUrl);
            });

            newUrl = string.Empty;
            await ReloadAsync();
        });
    }

    private async Task DeleteAsync(int postedLocationId)
    {
        await RunBusyAsync(async () =>
        {
            await operationRunner.RunAsync(
                (PostedLocationService service) => service.DeleteAsync(postedLocationId)
            );
            await ReloadAsync();
        });
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
}
