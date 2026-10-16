using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.FirstStartSetup.Status;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class FirstStartSetupStatusRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    [Test]
    public async Task GetDatabaseStatusAsync_EmptyDatabase_ReturnsNothingExisting()
    {
        // Arrange
        var repository = new FirstStartSetupStatusRepository(ReadDbContext);

        // Act
        var result = await repository.GetDatabaseStatusAsync(CancellationToken.None);

        // Assert
        result.ShouldBe(
            new FirstStartSetupDatabaseStatus(
                HasHosterRegistration: false,
                HasReleaseTemplate: false,
                HasReleaseFolderAutomationOrRemoteSourceAutomation: false,
                HasRelease: false,
                OptionalSetupStatus: new FirstStartOptionalSetupStatus(
                    HasNfoDatabaseOrMediaDatabaseRegistration: false,
                    HasImageHosterRegistration: false,
                    HasLinkCrypterRegistration: false,
                    HasConnectedTelegramConfiguration: false,
                    HasDistributionSiteRegistration: false,
                    HasProxyServer: false
                )
            )
        );
    }

    [Test]
    public async Task GetDatabaseStatusAsync_OneOfEachEntity_ReturnsEverythingExisting()
    {
        // Arrange
        var releaseGroup = new ReleaseGroup { Name = "Group" };
        var releaseTemplate = new ReleaseTemplate
        {
            Name = "Template",
            ReleaseType = ReleaseType.Managed,
            ReleaseContentType = ReleaseContentType.Movie,
            ReleaseGroup = releaseGroup,
        };
        DbContext.HosterRegistrations.Add(
            new HosterRegistration
            {
                Name = "Hoster",
                HosterClassName = "Rapidgator",
                SerializedConfig = "{}",
            }
        );
        DbContext.ReleaseFolderAutomations.Add(
            new ReleaseFolderAutomation
            {
                BasePath = "/data/incoming",
                ReleaseTemplate = releaseTemplate,
            }
        );
        DbContext.RemoteSourceAutomations.Add(
            new RemoteSourceAutomation
            {
                Name = "Seedbox automation",
                RemoteSourceRegistration = new RemoteSourceRegistration
                {
                    Name = "Seedbox",
                    SourceClassName = "Ftp",
                    SerializedConfig = "{}",
                },
                RemotePath = "/complete",
                TargetPath = "/data/downloads",
                ReleaseTemplate = releaseTemplate,
            }
        );
        DbContext.Releases.Add(
            new Release
            {
                Name = "My.Release",
                CreatedAt = DateTime.UtcNow,
                ReleaseType = ReleaseType.Managed,
                ReleaseFolderPath = "/data/My.Release",
                ReleaseGroup = releaseGroup,
            }
        );
        DbContext.NfoDatabaseRegistrations.Add(
            new NfoDatabaseRegistration { NfoDatabaseClassName = "SrrDb", SerializedConfig = "{}" }
        );
        DbContext.MediaDatabaseRegistrations.Add(
            new MediaDatabaseRegistration
            {
                MediaDatabaseClassName = "Tmdb",
                SerializedConfig = "{}",
            }
        );
        DbContext.ImageHosterRegistrations.Add(
            new ImageHosterRegistration
            {
                Name = "Image hoster",
                ImageHosterClassName = "ImgBb",
                SerializedConfig = "{}",
            }
        );
        DbContext.LinkCrypterRegistrations.Add(
            new LinkCrypterRegistration
            {
                Name = "Crypter",
                LinkCrypterClassName = "FileCrypt",
                SerializedConfig = "{}",
            }
        );
        DbContext.TelegramConfigurations.Add(
            new TelegramConfiguration
            {
                EncryptedBotToken = "encrypted",
                BotUsername = "bearcat_bot",
                NotificationBaseUrl = "http://localhost",
                ChatId = 42,
            }
        );
        DbContext.DistributionSiteRegistrations.Add(
            new DistributionSiteRegistration
            {
                Name = "Forum",
                DistributionSiteClassName = "XenForo",
                SerializedConfig = "{}",
            }
        );
        DbContext.ProxyServers.Add(
            new ProxyServer
            {
                Name = "Proxy",
                ProxyType = ProxyType.Http,
                Host = "proxy.local",
                Port = 8080,
            }
        );
        await DbContext.SaveChangesAsync();
        var repository = new FirstStartSetupStatusRepository(ReadDbContext);

        // Act
        var result = await repository.GetDatabaseStatusAsync(CancellationToken.None);

        // Assert
        result.ShouldBe(
            new FirstStartSetupDatabaseStatus(
                HasHosterRegistration: true,
                HasReleaseTemplate: true,
                HasReleaseFolderAutomationOrRemoteSourceAutomation: true,
                HasRelease: true,
                OptionalSetupStatus: new FirstStartOptionalSetupStatus(
                    HasNfoDatabaseOrMediaDatabaseRegistration: true,
                    HasImageHosterRegistration: true,
                    HasLinkCrypterRegistration: true,
                    HasConnectedTelegramConfiguration: true,
                    HasDistributionSiteRegistration: true,
                    HasProxyServer: true
                )
            )
        );
    }
}
