using Bearcat.Domain.UseCases.FirstStartSetup.Status;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.Setup;

public partial class FirstStartSetupIncompleteBanner : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public FirstStartSetupDatabaseStatus SetupStatus { get; set; } = null!;

    private string MissingPartsText =>
        L["FirstStartSetupIncomplete", string.Join(", ", GetMissingParts())];

    private List<string> GetMissingParts()
    {
        List<string> missingParts = [];

        if (!SetupStatus.HasHosterRegistration)
        {
            missingParts.Add(L["Hoster"]);
        }

        if (!SetupStatus.HasReleaseTemplate)
        {
            missingParts.Add(L["ReleaseTemplate"]);
        }

        if (!SetupStatus.HasReleaseFolderAutomationOrRemoteSourceAutomation)
        {
            missingParts.Add(L["FirstStartSetupStepSource"]);
        }

        return missingParts;
    }
}
