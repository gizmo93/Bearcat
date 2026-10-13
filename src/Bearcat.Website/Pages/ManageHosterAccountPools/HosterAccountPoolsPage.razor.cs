using Bearcat.Domain.UseCases.ManageHosterAccountPools;
using Bearcat.Domain.UseCases.ManageHosterAccountPools.ReadModels;
using Bearcat.Domain.UseCases.ManageHosterAccountPools.Repositories;
using Bearcat.Domain.UseCases.ManageHosters;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;

namespace Bearcat.Website.Pages.ManageHosterAccountPools;

public partial class HosterAccountPoolsPage(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner
)
{
    private const string PoolGridClass =
        "lg:grid-cols-[minmax(0,1fr)_minmax(0,0.8fr)_minmax(0,2fr)_8rem_11rem]";

    private readonly Dictionary<
        int,
        Dictionary<int, StorageQuotaCheckResult>
    > storageQuotaResultsByPoolId = [];
    private readonly HashSet<int> checkingStoragePoolIds = [];
    private IReadOnlyList<HosterAccountPoolReadModel> pools = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadPoolsAsync();
    }

    private async Task LoadPoolsAsync()
    {
        pools = await operationRunner.RunAsync(
            (IHosterAccountPoolReadRepository repository) => repository.GetAllAsync()
        );
    }

    private async Task ShowAddDialogAsync()
    {
        await ShowDialogAsync(new HosterAccountPoolFormModel(), L["NewHosterAccountPool"]);
    }

    private async Task ShowEditDialogAsync(HosterAccountPoolReadModel pool)
    {
        var detail = await operationRunner.RunAsync(
            (IHosterAccountPoolReadRepository repository) => repository.GetDetailAsync(pool.Id)
        );

        if (detail is null)
        {
            await LoadPoolsAsync();
            return;
        }

        await ShowDialogAsync(
            HosterAccountPoolFormModel.FromReadModel(detail),
            L["EditNamedItem", detail.Name]
        );
    }

    private async Task ShowDialogAsync(HosterAccountPoolFormModel formModel, string title)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditHosterAccountPoolDialog.FormModel)] = formModel,
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditHosterAccountPoolDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = title,
                Description = L["HosterAccountPoolDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadPoolsAsync();
        }
    }

    private async Task CheckStorageAsync(HosterAccountPoolReadModel pool)
    {
        checkingStoragePoolIds.Add(pool.Id);

        try
        {
            var results = await Task.WhenAll(
                pool.MembersInPriorityOrder.Select(async member =>
                    (
                        member.HosterRegistrationId,
                        Result: await operationRunner.RunAsync(
                            (HosterRegistrationService service) =>
                                service.GetStorageQuotaAsync(member.HosterRegistrationId)
                        )
                    )
                )
            );

            storageQuotaResultsByPoolId[pool.Id] = results.ToDictionary(
                result => result.HosterRegistrationId,
                result => result.Result
            );
        }
        finally
        {
            checkingStoragePoolIds.Remove(pool.Id);
        }
    }

    private async Task DeleteAsync(HosterAccountPoolReadModel pool)
    {
        var confirmation = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", pool.Name],
            L["DeleteHosterAccountPoolConfirmation", pool.Name],
            new ConfirmDialogOptions
            {
                ConfirmText = L["Delete"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        if (!confirmation.Confirmed)
        {
            return;
        }

        await operationRunner.RunAsync(
            (HosterAccountPoolService service) => service.DeleteAsync(pool.Id)
        );

        storageQuotaResultsByPoolId.Remove(pool.Id);
        toastService.Success(L["HosterAccountPoolDeleted", pool.Name]);
        await LoadPoolsAsync();
    }
}
