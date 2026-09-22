using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Bearcat.Website.Shared;

public partial class CopyButton(IJSRuntime jsRuntime, ToastService toastService) : ComponentBase
{
    [Parameter]
    public string? Value { get; set; }

    [Parameter]
    [EditorRequired]
    public string Label { get; set; } = null!;

    [Parameter]
    public bool ShowLabel { get; set; }

    [Parameter]
    public string Icon { get; set; } = "copy";

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public string? Class { get; set; }

    private string? ButtonClass => ShowLabel ? Class : $"{Class} bearcat-copy-icon-button".Trim();

    private async Task CopyAsync()
    {
        try
        {
            var result = await jsRuntime.InvokeAsync<bool?>("bearcat.takeCopyResult");
            var copied = result ?? await jsRuntime.InvokeAsync<bool>("bearcat.copyText", Value);

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
