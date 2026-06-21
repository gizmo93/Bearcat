using Bearcat.Domain.UseCases.ManagePostedLocations;
using Bearcat.Domain.UseCases.ManagePostedLocations.ReadModels;
using Bearcat.Domain.UseCases.ManagePostedLocations.Repositories;
using Bearcat.Website.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManagePostedLocations;

public partial class PostedLocations : OwningComponentBase, IReloadableComponent
{
    [Parameter]
    public int? ReleaseId { get; set; }

    [Parameter]
    public int? ReleaseCollectionId { get; set; }

    private IPostedLocationReadRepository readRepository = null!;
    private IReadOnlyList<PostedLocationReadModel> locations = [];
    private string newUrl = string.Empty;
    private bool isBusy;
    private string? errorMessage;

    protected override async Task OnInitializedAsync()
    {
        readRepository = ScopedServices.GetRequiredService<IPostedLocationReadRepository>();
        await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        locations = ReleaseCollectionId is { } collectionId
            ? await readRepository.GetForCollectionAsync(collectionId)
            : await readRepository.GetForReleaseAsync(ReleaseId!.Value);

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
            var service = ScopedServices.GetRequiredService<PostedLocationService>();

            if (ReleaseCollectionId is { } collectionId)
            {
                await service.AddForCollectionAsync(collectionId, newUrl);
            }
            else
            {
                await service.AddForReleaseAsync(ReleaseId!.Value, newUrl);
            }

            newUrl = string.Empty;
            await ReloadAsync();
        });
    }

    private async Task DeleteAsync(int postedLocationId)
    {
        await RunBusyAsync(async () =>
        {
            var service = ScopedServices.GetRequiredService<PostedLocationService>();
            await service.DeleteAsync(postedLocationId);
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
