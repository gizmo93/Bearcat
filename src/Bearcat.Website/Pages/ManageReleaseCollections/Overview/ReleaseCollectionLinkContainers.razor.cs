using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Website.Pages.ManageReleases.Overview;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Bearcat.Website.Pages.ManageReleaseCollections.Overview;

public partial class ReleaseCollectionLinkContainers(
    IJSRuntime jsRuntime,
    ToastService toastService
) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<CollectionUploadSlotReadModel> UploadSlots { get; set; } = [];

    [Parameter]
    public EventCallback<CollectionUploadSlotContainerReadModel> OnDeleteFailedContainerRequested { get; set; }

    private IReadOnlyList<LinkCrypterContainerUrls> containerUrlsByLinkCrypter = [];

    private IReadOnlyList<CollectionUploadSlotReadModel> UploadSlotsWithLinkContainers =>
        UploadSlots
            .Where(CollectionLinkContainerOverviewService.HasSharedLinkCryptersOrContainers)
            .ToList();

    private int ContainerCount => UploadSlots.Sum(uploadSlot => uploadSlot.Containers.Count);

    protected override void OnParametersSet()
    {
        containerUrlsByLinkCrypter =
            CollectionLinkContainerOverviewService.GroupCreatedContainerUrlsByLinkCrypter(
                UploadSlots
            );
    }

    private static string GetCoverageText(
        CollectionUploadSlotContainerReadModel container,
        CollectionUploadSlotReadModel uploadSlot
    ) => $"{container.SourceUploadCount}/{uploadSlot.UploadConfigCount}";

    private string GetCoverageTitle(
        CollectionUploadSlotContainerReadModel container,
        CollectionUploadSlotReadModel uploadSlot
    ) => L["LinkContainerCoverageTitle", container.SourceUploadCount, uploadSlot.UploadConfigCount];

    private static string JoinLines(IReadOnlyList<string> lines) =>
        string.Join(Environment.NewLine, lines);

    private async Task CopyTextAsync(string text)
    {
        try
        {
            var result = await jsRuntime.InvokeAsync<bool?>("bearcat.takeCopyResult");
            var copied = result ?? await jsRuntime.InvokeAsync<bool>("bearcat.copyText", text);

            if (copied)
            {
                toastService.Success(L["Copied"]);
            }
            else
            {
                toastService.Error(L["CopyFailed"]);
            }
        }
        catch (JSException)
        {
            toastService.Error(L["CopyFailed"]);
        }
    }
}
