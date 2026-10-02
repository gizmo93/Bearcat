using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageHosters;

public class HosterConfigurationRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string HosterClassName = "TestHoster";

    private HosterConfigurationRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        var hosterMock = new Mock<IHoster>();
        hosterMock.Setup(hoster => hoster.Name).Returns("Test hoster");
        var hosterFactoryMock = new Mock<IHosterFactory>(MockBehavior.Strict);
        hosterFactoryMock
            .Setup(factory => factory.GetHostersByName())
            .Returns(new Dictionary<string, IHoster> { [HosterClassName] = hosterMock.Object });

        repository = new HosterConfigurationRepository(
            DbContext,
            DbContext,
            hosterFactoryMock.Object
        );
    }

    [Test]
    public async Task GetAllRegistrationsAsync_SpeedLimitsAndProxyServers_ReturnsExactValuesOrderedByName()
    {
        // Arrange
        var uploadProxyServer = CreateProxyServer("Upload proxy", "upload.proxy.test");
        var mirrorProxyServer = CreateProxyServer("Mirror proxy", "mirror.proxy.test");
        DbContext.ProxyServers.AddRange(uploadProxyServer, mirrorProxyServer);
        await DbContext.SaveChangesAsync();

        var gamma = CreateRegistration("Gamma hoster", 10m, 2.5m);
        gamma.MirrorDownloadProxySelection = ProxySelection.SpecificProxyServer;
        gamma.MirrorDownloadProxyServerId = mirrorProxyServer.Id;
        var alpha = CreateRegistration("Alpha hoster", 12.345m, null);
        alpha.UploadProxySelection = ProxySelection.SpecificProxyServer;
        alpha.UploadProxyServerId = uploadProxyServer.Id;
        alpha.MirrorDownloadProxySelection = ProxySelection.SpecificProxyServer;
        alpha.MirrorDownloadProxyServerId = mirrorProxyServer.Id;
        var beta = CreateRegistration("Beta hoster", null, 0.75m);
        beta.UploadProxySelection = ProxySelection.NoProxy;
        var delta = CreateRegistration("Delta hoster", 9999999.999m, 0.001m);
        DbContext.HosterRegistrations.AddRange(gamma, alpha, beta, delta);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetAllRegistrationsAsync(CancellationToken.None);

        // Assert
        result
            .Select(r => r.Name)
            .ShouldBe(["Alpha hoster", "Beta hoster", "Delta hoster", "Gamma hoster"]);

        var alphaResult = result[0];
        alphaResult.Id.ShouldBe(alpha.Id);
        alphaResult.UploadSpeedLimitMegabytesPerSecond.ShouldBe(12.345m);
        alphaResult.MirrorDownloadSpeedLimitMegabytesPerSecond.ShouldBeNull();
        alphaResult.UploadProxySelection.ShouldBe(ProxySelection.SpecificProxyServer);
        alphaResult.UploadProxyServerId.ShouldBe(uploadProxyServer.Id);
        alphaResult.UploadProxyServerName.ShouldBe("Upload proxy");
        alphaResult.MirrorDownloadProxySelection.ShouldBe(ProxySelection.SpecificProxyServer);
        alphaResult.MirrorDownloadProxyServerId.ShouldBe(mirrorProxyServer.Id);
        alphaResult.MirrorDownloadProxyServerName.ShouldBe("Mirror proxy");
        alphaResult.HosterName.ShouldBe("Test hoster");
        alphaResult.FullClassName.ShouldBe(HosterClassName);

        var betaResult = result[1];
        betaResult.UploadSpeedLimitMegabytesPerSecond.ShouldBeNull();
        betaResult.MirrorDownloadSpeedLimitMegabytesPerSecond.ShouldBe(0.75m);
        betaResult.UploadProxySelection.ShouldBe(ProxySelection.NoProxy);
        betaResult.UploadProxyServerId.ShouldBeNull();
        betaResult.UploadProxyServerName.ShouldBeNull();
        betaResult.MirrorDownloadProxyServerId.ShouldBeNull();
        betaResult.MirrorDownloadProxyServerName.ShouldBeNull();

        var deltaResult = result[2];
        deltaResult.UploadSpeedLimitMegabytesPerSecond.ShouldBe(9999999.999m);
        deltaResult.MirrorDownloadSpeedLimitMegabytesPerSecond.ShouldBe(0.001m);

        var gammaResult = result[3];
        gammaResult.UploadSpeedLimitMegabytesPerSecond.ShouldBe(10m);
        gammaResult.MirrorDownloadSpeedLimitMegabytesPerSecond.ShouldBe(2.5m);
        gammaResult.UploadProxyServerName.ShouldBeNull();
        gammaResult.MirrorDownloadProxyServerId.ShouldBe(mirrorProxyServer.Id);
        gammaResult.MirrorDownloadProxyServerName.ShouldBe("Mirror proxy");
    }

    [Test]
    public async Task GetAllRegistrationsAsync_OverridesSet_ReturnsStoredOverrides()
    {
        // Arrange
        var registration = CreateRegistration("Alpha hoster", null, null);
        registration.IsActive = false;
        registration.HasUnreadableSecrets = true;
        registration.RequiresCaptchaVerification = true;
        registration.MaxParallelUploadsOverride = 3;
        registration.NumberOfHoursUntilReuploadOverride = 48;
        registration.ReuploadTriggerOverride = ReuploadTrigger.OnlyWhenFullyOffline;
        registration.AlwaysReuploadAllFiles = true;
        registration.UseForMirrorDownloads = true;
        registration.MirrorPriority = 7;
        DbContext.HosterRegistrations.Add(registration);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetAllRegistrationsAsync(CancellationToken.None);

        // Assert
        var readModel = result.ShouldHaveSingleItem();
        readModel.IsActive.ShouldBeFalse();
        readModel.HasUnreadableSecrets.ShouldBeTrue();
        readModel.RequiresCaptchaVerification.ShouldBeTrue();
        readModel.MaxParallelUploadsOverride.ShouldBe(3);
        readModel.NumberOfHoursUntilReuploadOverride.ShouldBe(48);
        readModel.ReuploadTriggerOverride.ShouldBe(ReuploadTrigger.OnlyWhenFullyOffline);
        readModel.AlwaysReuploadAllFiles.ShouldBeTrue();
        readModel.UseForMirrorDownloads.ShouldBeTrue();
        readModel.MirrorPriority.ShouldBe(7);
    }

    private static HosterRegistration CreateRegistration(
        string name,
        decimal? uploadSpeedLimitMegabytesPerSecond,
        decimal? mirrorDownloadSpeedLimitMegabytesPerSecond
    )
    {
        return new HosterRegistration
        {
            Name = name,
            SerializedConfig = "{}",
            HosterClassName = HosterClassName,
            IsActive = true,
            UploadSpeedLimitMegabytesPerSecond = uploadSpeedLimitMegabytesPerSecond,
            MirrorDownloadSpeedLimitMegabytesPerSecond = mirrorDownloadSpeedLimitMegabytesPerSecond,
        };
    }

    private static ProxyServer CreateProxyServer(string name, string host)
    {
        return new ProxyServer
        {
            Name = name,
            ProxyType = ProxyType.Http,
            Host = host,
            Port = 8080,
        };
    }
}
