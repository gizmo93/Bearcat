using Bearcat.Domain.UseCases.ManageReleaseGroups.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Bearcat.Website.Pages.ManageReleases.Results;

public partial class ReleaseBulkActionBar(
    IScopedOperationRunner operationRunner,
    ToastService toastService,
    IJSRuntime jsRuntime
) : ComponentBase, IAsyncDisposable
{
    private DotNetObjectReference<ReleaseBulkActionBar>? dotNetReference;
    private IJSObjectReference? escapeKeyHandle;

    [Parameter]
    [EditorRequired]
    public ReleaseSelection Selection { get; set; } = null!;

    [Parameter]
    public IReadOnlyList<int> VisibleReleaseIds { get; set; } = [];

    [Parameter]
    public IReadOnlyList<ReleaseGroupReadModel> ReleaseGroups { get; set; } = [];

    [Parameter]
    public EventCallback OnSelectionChanged { get; set; }

    [Parameter]
    public EventCallback OnReleasesUpdated { get; set; }

    private IReadOnlyList<SelectOption<int>> ReleaseGroupOptions =>
        ReleaseGroups
            .Select(group => new SelectOption<int>(group.ReleaseGroupId, group.Name))
            .ToList();

    private IReadOnlyList<SelectOption<string>> PrimaryLanguageOptions =>
        PrimaryLanguageSelectOptions.Create(L["NotSet"]);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        dotNetReference = DotNetObjectReference.Create(this);

        try
        {
            escapeKeyHandle = await jsRuntime.InvokeAsync<IJSObjectReference>(
                "bearcat.selectionEscapeKey.attach",
                dotNetReference
            );
        }
        catch (JSDisconnectedException) { }
    }

    [JSInvokable]
    public async Task ClearSelectionFromEscapeKeyAsync()
    {
        if (Selection.Count == 0)
        {
            return;
        }

        await ClearSelectionAsync();
    }

    private async Task SelectAllOnPageAsync()
    {
        Selection.SelectAll(VisibleReleaseIds);
        await OnSelectionChanged.InvokeAsync();
    }

    private async Task ClearSelectionAsync()
    {
        Selection.Clear();
        await OnSelectionChanged.InvokeAsync();
    }

    private async Task ApplyReleaseGroupAsync(int releaseGroupId)
    {
        var releaseIds = Selection.SelectedReleaseIds;

        await operationRunner.RunAsync(
            (ReleaseService service) => service.UpdateReleaseGroupAsync(releaseIds, releaseGroupId)
        );

        toastService.Success(L["ReleaseGroupChangedForReleases", releaseIds.Count]);
        Selection.Clear();
        await OnReleasesUpdated.InvokeAsync();
    }

    private async Task ApplyPrimaryLanguageAsync(string primaryLanguageCode)
    {
        var releaseIds = Selection.SelectedReleaseIds;

        await operationRunner.RunAsync(
            (ReleaseService service) =>
                service.UpdatePrimaryLanguageAsync(releaseIds, primaryLanguageCode)
        );

        toastService.Success(L["PrimaryLanguageChangedForReleases", releaseIds.Count]);
        Selection.Clear();
        await OnReleasesUpdated.InvokeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (escapeKeyHandle is not null)
        {
            try
            {
                await escapeKeyHandle.InvokeVoidAsync("detach");
                await escapeKeyHandle.DisposeAsync();
            }
            catch (JSDisconnectedException) { }
        }

        dotNetReference?.Dispose();
    }
}
