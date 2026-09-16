using Bearcat.Domain.UseCases.ManagePostedLocations;
using Bearcat.Domain.UseCases.ManagePostedLocations.ReadModels;
using Bearcat.Domain.UseCases.ManagePostedLocations.Repositories;
using Bearcat.Domain.UseCases.PostToForums;
using Bearcat.Domain.UseCases.PostToForums.Models;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManagePostedLocations;

public partial class PostedLocations(
    IScopedOperationRunner operationRunner,
    DialogService dialogService,
    ToastService toastService
) : ComponentBase, IReloadableComponent
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

    private async Task UpdateAsync(PostedLocationReadModel location)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(UpdatePostedLocationDialog.PostedUrl)] = location.Url,
            [nameof(UpdatePostedLocationDialog.ForumPostTemplateId)] = location.ForumPostTemplateId,
            [nameof(UpdatePostedLocationDialog.TemplateType)] = ReleaseCollectionId is null
                ? ForumPostTemplateType.Release
                : ForumPostTemplateType.ReleaseCollection,
        };

        var dialog = await dialogService.OpenAsync<UpdatePostedLocationDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["UpdatePostedLocationTitle"],
                Description = L["UpdatePostedLocationDescription"],
                ShowClose = true,
            }
        );

        if (dialog.Cancelled)
        {
            return;
        }

        var forumPostTemplateId = dialog.GetData<int>();

        await RunBusyAsync(async () =>
        {
            var result = await operationRunner.RunAsync(
                (AutoForumPostingService service) =>
                    service.UpdatePostedLocationAsync(
                        location.PostedLocationId,
                        forumPostTemplateId
                    )
            );

            if (result.Status != AutoPostUpdateStatus.Updated)
            {
                errorMessage = string.Join(" ", result.Errors);
                return;
            }

            toastService.Success(L["UpdatePostedLocationSucceeded"]);
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
