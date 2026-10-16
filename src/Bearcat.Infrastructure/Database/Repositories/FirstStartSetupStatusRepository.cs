using Bearcat.Domain.UseCases.FirstStartSetup.Repositories;
using Bearcat.Domain.UseCases.FirstStartSetup.Status;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class FirstStartSetupStatusRepository(IBearcatReadDbContext dbRead)
    : IFirstStartSetupStatusRepository
{
    private const int HosterRegistrationMarker = 1;
    private const int ReleaseTemplateMarker = 2;
    private const int ReleaseFolderAutomationMarker = 3;
    private const int RemoteSourceAutomationMarker = 4;
    private const int ReleaseMarker = 5;
    private const int NfoDatabaseRegistrationMarker = 6;
    private const int MediaDatabaseRegistrationMarker = 7;
    private const int ImageHosterRegistrationMarker = 8;
    private const int LinkCrypterRegistrationMarker = 9;
    private const int ConnectedTelegramConfigurationMarker = 10;
    private const int DistributionSiteRegistrationMarker = 11;
    private const int ProxyServerMarker = 12;

    public async Task<FirstStartSetupDatabaseStatus> GetDatabaseStatusAsync(
        CancellationToken cancellationToken = default
    )
    {
        var existingEntityMarkers = await dbRead
            .HosterRegistrations.OrderBy(hosterRegistration => hosterRegistration.Id)
            .Select(hosterRegistration => HosterRegistrationMarker)
            .Take(1)
            .Concat(
                dbRead
                    .ReleaseTemplates.OrderBy(releaseTemplate => releaseTemplate.Id)
                    .Select(releaseTemplate => ReleaseTemplateMarker)
                    .Take(1)
            )
            .Concat(
                dbRead
                    .ReleaseFolderAutomations.OrderBy(automation => automation.Id)
                    .Select(automation => ReleaseFolderAutomationMarker)
                    .Take(1)
            )
            .Concat(
                dbRead
                    .RemoteSourceAutomations.OrderBy(automation => automation.Id)
                    .Select(automation => RemoteSourceAutomationMarker)
                    .Take(1)
            )
            .Concat(
                dbRead
                    .Releases.OrderBy(release => release.Id)
                    .Select(release => ReleaseMarker)
                    .Take(1)
            )
            .Concat(
                dbRead
                    .NfoDatabaseRegistrations.OrderBy(registration => registration.Id)
                    .Select(registration => NfoDatabaseRegistrationMarker)
                    .Take(1)
            )
            .Concat(
                dbRead
                    .MediaDatabaseRegistrations.OrderBy(registration => registration.Id)
                    .Select(registration => MediaDatabaseRegistrationMarker)
                    .Take(1)
            )
            .Concat(
                dbRead
                    .ImageHosterRegistrations.OrderBy(registration => registration.Id)
                    .Select(registration => ImageHosterRegistrationMarker)
                    .Take(1)
            )
            .Concat(
                dbRead
                    .LinkCrypterRegistrations.OrderBy(registration => registration.Id)
                    .Select(registration => LinkCrypterRegistrationMarker)
                    .Take(1)
            )
            .Concat(
                dbRead
                    .TelegramConfigurations.Where(configuration => configuration.ChatId != null)
                    .OrderBy(configuration => configuration.Id)
                    .Select(configuration => ConnectedTelegramConfigurationMarker)
                    .Take(1)
            )
            .Concat(
                dbRead
                    .DistributionSiteRegistrations.OrderBy(registration => registration.Id)
                    .Select(registration => DistributionSiteRegistrationMarker)
                    .Take(1)
            )
            .Concat(
                dbRead
                    .ProxyServers.OrderBy(proxyServer => proxyServer.Id)
                    .Select(proxyServer => ProxyServerMarker)
                    .Take(1)
            )
            .ToListAsync(cancellationToken);

        return new FirstStartSetupDatabaseStatus(
            HasHosterRegistration: existingEntityMarkers.Contains(HosterRegistrationMarker),
            HasReleaseTemplate: existingEntityMarkers.Contains(ReleaseTemplateMarker),
            HasReleaseFolderAutomationOrRemoteSourceAutomation: existingEntityMarkers.Contains(
                ReleaseFolderAutomationMarker
            ) || existingEntityMarkers.Contains(RemoteSourceAutomationMarker),
            HasRelease: existingEntityMarkers.Contains(ReleaseMarker),
            HasNfoDatabaseOrMediaDatabaseRegistration: existingEntityMarkers.Contains(
                NfoDatabaseRegistrationMarker
            ) || existingEntityMarkers.Contains(MediaDatabaseRegistrationMarker),
            HasImageHosterRegistration: existingEntityMarkers.Contains(
                ImageHosterRegistrationMarker
            ),
            HasLinkCrypterRegistration: existingEntityMarkers.Contains(
                LinkCrypterRegistrationMarker
            ),
            HasConnectedTelegramConfiguration: existingEntityMarkers.Contains(
                ConnectedTelegramConfigurationMarker
            ),
            HasDistributionSiteRegistration: existingEntityMarkers.Contains(
                DistributionSiteRegistrationMarker
            ),
            HasProxyServer: existingEntityMarkers.Contains(ProxyServerMarker)
        );
    }
}
