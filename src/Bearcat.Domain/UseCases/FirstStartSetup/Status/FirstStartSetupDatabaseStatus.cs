namespace Bearcat.Domain.UseCases.FirstStartSetup.Status;

public sealed record FirstStartSetupDatabaseStatus(
    bool HasHosterRegistration,
    bool HasReleaseTemplate,
    bool HasReleaseFolderAutomationOrRemoteSourceAutomation,
    bool HasRelease,
    bool HasNfoDatabaseOrMediaDatabaseRegistration,
    bool HasImageHosterRegistration,
    bool HasLinkCrypterRegistration,
    bool HasConnectedTelegramConfiguration,
    bool HasDistributionSiteRegistration,
    bool HasProxyServer
)
{
    public bool IsFreshInstallation => !HasHosterRegistration && !HasRelease;

    public bool IsRequiredSetupComplete =>
        HasHosterRegistration
        && HasReleaseTemplate
        && HasReleaseFolderAutomationOrRemoteSourceAutomation;
}
