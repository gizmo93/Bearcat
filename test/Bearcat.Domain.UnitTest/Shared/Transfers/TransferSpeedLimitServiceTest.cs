using System.Linq.Expressions;
using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Transfers;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.Transfers;
using Moq;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared.Transfers;

public class TransferSpeedLimitServiceTest
{
    private static readonly HosterRegistration Registration = new()
    {
        Id = 1,
        Name = "Hoster",
        SerializedConfig = "{}",
        HosterClassName = "Hoster",
    };

    private decimal? uploadSpeedLimitMegabytesPerSecond;

    private decimal? downloadSpeedLimitMegabytesPerSecond;

    private TransferSpeedLimitService service = null!;

    [SetUp]
    public void SetUp()
    {
        uploadSpeedLimitMegabytesPerSecond = null;
        downloadSpeedLimitMegabytesPerSecond = null;

        var configurationProviderMock = new Mock<IApplicationConfigurationProvider>(
            MockBehavior.Strict
        );
        configurationProviderMock
            .Setup(provider =>
                provider.GetValue(
                    It.IsAny<Expression<Func<UploadConcurrencyConfiguration, decimal?>>>()
                )
            )
            .Returns(() => uploadSpeedLimitMegabytesPerSecond);
        configurationProviderMock
            .Setup(provider =>
                provider.GetValue(It.IsAny<Expression<Func<DownloadConfiguration, decimal?>>>())
            )
            .Returns(() => downloadSpeedLimitMegabytesPerSecond);

        service = new TransferSpeedLimitService(configurationProviderMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        service.Dispose();
    }

    [Test]
    public void EnterUploadScope_NoSpeedLimitConfigured_CurrentIsNull()
    {
        // Act
        using var scope = service.EnterUploadScope(Registration);

        // Assert
        TransferSpeedLimitScope.Current.ShouldBeNull();
    }

    [Test]
    public void EnterUploadScope_ZeroSpeedLimitConfigured_CurrentIsNull()
    {
        // Arrange
        uploadSpeedLimitMegabytesPerSecond = 0;

        // Act
        using var scope = service.EnterUploadScope(Registration);

        // Assert
        TransferSpeedLimitScope.Current.ShouldBeNull();
    }

    [Test]
    public void EnterUploadScope_SpeedLimitConfigured_ScopeContainsLimiterWithConfiguredRate()
    {
        // Arrange
        uploadSpeedLimitMegabytesPerSecond = 0.5m;

        // Act
        using var scope = service.EnterUploadScope(Registration);

        // Assert
        var limiters = TransferSpeedLimitScope.Current.ShouldNotBeNull();
        limiters.Count.ShouldBe(1);
        limiters[0].BytesPerSecond.ShouldBe(524_288);
    }

    [Test]
    public void EnterUploadScope_SpeedLimitChanged_ReusesLimiterWithNewRate()
    {
        // Arrange
        uploadSpeedLimitMegabytesPerSecond = 50;
        TransferSpeedLimiter firstLimiter;

        using (service.EnterUploadScope(Registration))
        {
            firstLimiter = TransferSpeedLimitScope.Current.ShouldNotBeNull()[0];
        }

        uploadSpeedLimitMegabytesPerSecond = 2;

        // Act
        using var scope = service.EnterUploadScope(Registration);

        // Assert
        var secondLimiter = TransferSpeedLimitScope.Current.ShouldNotBeNull()[0];
        secondLimiter.ShouldBeSameAs(firstLimiter);
        secondLimiter.BytesPerSecond.ShouldBe(2 * 1024 * 1024);
    }

    [Test]
    public void EnterUploadScope_SpeedLimitExceedsIntRange_ClampsToIntMaxValue()
    {
        // Arrange
        uploadSpeedLimitMegabytesPerSecond = decimal.MaxValue;

        // Act
        using var scope = service.EnterUploadScope(Registration);

        // Assert
        TransferSpeedLimitScope.Current.ShouldNotBeNull()[0].BytesPerSecond.ShouldBe(int.MaxValue);
    }

    [Test]
    public void EnterUploadScope_SpeedLimitBelowOneByte_ClampsToOneByte()
    {
        // Arrange
        uploadSpeedLimitMegabytesPerSecond = 0.0000001m;

        // Act
        using var scope = service.EnterUploadScope(Registration);

        // Assert
        TransferSpeedLimitScope.Current.ShouldNotBeNull()[0].BytesPerSecond.ShouldBe(1);
    }

    [Test]
    public void EnterUploadScope_ScopeDisposed_RestoresPreviousScope()
    {
        // Arrange
        uploadSpeedLimitMegabytesPerSecond = 50;

        // Act
        using (service.EnterUploadScope(Registration)) { }

        // Assert
        TransferSpeedLimitScope.Current.ShouldBeNull();
    }

    [Test]
    public void EnterMirrorDownloadScope_SpeedLimitConfigured_UsesDownloadConfiguration()
    {
        // Arrange
        uploadSpeedLimitMegabytesPerSecond = 50;
        downloadSpeedLimitMegabytesPerSecond = 1.5m;

        // Act
        using var scope = service.EnterMirrorDownloadScope(Registration);

        // Assert
        var limiters = TransferSpeedLimitScope.Current.ShouldNotBeNull();
        limiters.Count.ShouldBe(1);
        limiters[0].BytesPerSecond.ShouldBe(1_572_864);
    }
}
