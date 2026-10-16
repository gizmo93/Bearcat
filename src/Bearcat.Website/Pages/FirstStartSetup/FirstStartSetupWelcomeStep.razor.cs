using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.FirstStartSetup;

public partial class FirstStartSetupWelcomeStep : ComponentBase
{
    [Parameter]
    public EventCallback OnStart { get; set; }

    [Parameter]
    public EventCallback OnSkipSetup { get; set; }

    private IReadOnlyList<FirstStartSetupLifecycleStage> lifecycleStages = [];

    protected override void OnInitialized()
    {
        lifecycleStages =
        [
            new("folder", L["Folder"]),
            new("package", L["Release"]),
            new("archive", L["Archive"]),
            new("upload", L["Upload"]),
            new("link", L["FirstStartSetupLinkCheck"]),
            new("refresh-cw", L["Reupload"]),
        ];
    }
}
