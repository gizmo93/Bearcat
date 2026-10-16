using Bearcat.Abstractions.Hoster.Results;

namespace Bearcat.Website.Pages.FirstStartSetup;

public class FirstStartSetupWizardState
{
    public HashSet<int> SelectedHosterRegistrationIds { get; } = [];

    public Dictionary<int, TryLoginResult> LoginResultsByHosterRegistrationId { get; } = [];

    public int? ReleaseTemplateId { get; set; }

    public bool HasSelectedHosterRegistrationWithSuccessfulLogin()
    {
        return SelectedHosterRegistrationIds.Any(hosterRegistrationId =>
            LoginResultsByHosterRegistrationId.TryGetValue(
                hosterRegistrationId,
                out var loginResult
            ) && loginResult.IsSuccess
        );
    }
}
