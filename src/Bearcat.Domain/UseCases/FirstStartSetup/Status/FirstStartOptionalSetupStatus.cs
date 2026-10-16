namespace Bearcat.Domain.UseCases.FirstStartSetup.Status;

public sealed record FirstStartOptionalSetupStatus(
    bool HasNfoDatabaseOrMediaDatabaseRegistration,
    bool HasImageHosterRegistration,
    bool HasLinkCrypterRegistration,
    bool HasConnectedTelegramConfiguration,
    bool HasDistributionSiteRegistration,
    bool HasProxyServer
)
{
    public bool IsEverythingSetUp =>
        HasNfoDatabaseOrMediaDatabaseRegistration
        && HasImageHosterRegistration
        && HasLinkCrypterRegistration
        && HasConnectedTelegramConfiguration
        && HasDistributionSiteRegistration
        && HasProxyServer;
}
