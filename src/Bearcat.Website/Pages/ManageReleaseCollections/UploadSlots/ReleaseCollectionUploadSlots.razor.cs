using System.Globalization;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Formatting;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Website.Pages.ManageReleaseCollections.UploadSlots;

public partial class ReleaseCollectionUploadSlots(
    DialogService dialogService,
    IScopedOperationRunner operationRunner,
    TimeProvider timeProvider
) : ComponentBase
{
    private const int MaximumVisibleLinkCrypterCount = 3;

    [Parameter]
    [EditorRequired]
    public int ReleaseCollectionId { get; set; }

    [Parameter]
    [EditorRequired]
    public IReadOnlyList<CollectionUploadSlotReadModel> UploadSlots { get; set; } = [];

    [Parameter]
    [EditorRequired]
    public int ReleaseCount { get; set; }

    [Parameter]
    public EventCallback OnUploadSlotsChanged { get; set; }

    [Parameter]
    public EventCallback<CollectionUploadSlotContainerReadModel> OnDeleteFailedContainerRequested { get; set; }

    private readonly HashSet<int> knownUploadSlotIds = [];
    private readonly HashSet<int> expandedUploadSlotIds = [];

    protected override void OnParametersSet()
    {
        var newUploadSlots = UploadSlots
            .Where(uploadSlot => knownUploadSlotIds.Add(uploadSlot.CollectionUploadSlotId))
            .ToList();

        foreach (var uploadSlot in newUploadSlots.Where(slot => GetFailedContainerCount(slot) > 0))
        {
            expandedUploadSlotIds.Add(uploadSlot.CollectionUploadSlotId);
        }
    }

    private void ToggleDetails(int collectionUploadSlotId)
    {
        if (!expandedUploadSlotIds.Remove(collectionUploadSlotId))
        {
            expandedUploadSlotIds.Add(collectionUploadSlotId);
        }
    }

    private static int GetFailedContainerCount(CollectionUploadSlotReadModel uploadSlot) =>
        uploadSlot.Containers.Count(container =>
            container.State is LinkCrypterContainerState.CreationFailed
        );

    private static string GetHiddenLinkCrypterNames(CollectionUploadSlotReadModel uploadSlot) =>
        string.Join(
            ", ",
            uploadSlot
                .SharedLinkCrypters.Skip(MaximumVisibleLinkCrypterCount)
                .Select(linkCrypter => linkCrypter.LinkCrypterRegistrationName)
        );

    private static BadgeVariant GetContainerStateBadgeVariant(LinkCrypterContainerState state) =>
        state switch
        {
            LinkCrypterContainerState.Created => BadgeVariant.Secondary,
            LinkCrypterContainerState.CreationFailed => BadgeVariant.Destructive,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
        };

    private string HumanizeTimestamp(DateTime value) => timeProvider.Humanize(value);

    private static string FormatTimestamp(DateTime value) =>
        value.ToString("g", CultureInfo.CurrentCulture);

    private async Task ShowCreateUploadSlotDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateCollectionUploadSlotDialog.FormModel)] = new CollectionUploadSlotFormModel
            {
                ReleaseCollectionId = ReleaseCollectionId,
            },
            [nameof(CreateCollectionUploadSlotDialog.ExistingSlotKeys)] = UploadSlots
                .Select(uploadSlot => uploadSlot.Key)
                .ToList(),
        };

        var dialog = await dialogService.OpenAsync<CreateCollectionUploadSlotDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["NewCollectionUploadSlot"],
                Description = L["CreateCollectionUploadSlotDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await OnUploadSlotsChanged.InvokeAsync();
        }
    }

    private async Task ShowEditSharedLinkCryptersDialogAsync(
        CollectionUploadSlotReadModel uploadSlot
    )
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(EditCollectionUploadSlotLinkCryptersDialog.CollectionUploadSlotId)] =
                uploadSlot.CollectionUploadSlotId,
            [nameof(EditCollectionUploadSlotLinkCryptersDialog.SlotName)] = uploadSlot.Name,
            [nameof(EditCollectionUploadSlotLinkCryptersDialog.SharedLinkCrypters)] =
                uploadSlot.SharedLinkCrypters,
        };

        var dialog = await dialogService.OpenAsync<EditCollectionUploadSlotLinkCryptersDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditSharedLinkCrypters"],
                Description = L["SharedLinkCryptersDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await OnUploadSlotsChanged.InvokeAsync();
        }
    }

    private async Task DeleteUploadSlotAsync(CollectionUploadSlotReadModel uploadSlot)
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", uploadSlot.Name],
            L[
                "DeleteCollectionUploadSlotConfirmation",
                uploadSlot.Name,
                uploadSlot.UploadConfigCount,
                uploadSlot.UploadCount,
                uploadSlot.Containers.Count
            ],
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
            (ReleaseCollectionService service) =>
                service.DeleteUploadSlotAsync(uploadSlot.CollectionUploadSlotId)
        );
        await OnUploadSlotsChanged.InvokeAsync();
    }
}
