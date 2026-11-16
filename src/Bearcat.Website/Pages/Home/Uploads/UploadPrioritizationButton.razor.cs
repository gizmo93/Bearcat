using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.Uploads;

public partial class UploadPrioritizationButton : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public bool IsPrioritized { get; set; }

    [Parameter]
    [EditorRequired]
    public EventCallback OnClick { get; set; }

    private string Label => IsPrioritized ? L["RemoveUploadPrioritization"] : L["PrioritizeUpload"];
}
