using Bearcat.Abstractions.Updates;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Shared;

public partial class UpdateBanner(IUpdateChecker updateChecker) : ComponentBase
{
    private UpdateStatus? status;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        status = await updateChecker.GetUpdateStatusAsync();

        if (status.IsUpdateAvailable)
        {
            await InvokeAsync(StateHasChanged);
        }
    }
}
