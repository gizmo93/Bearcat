using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageHosters;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageHosters;

public class HosterRegistrationServiceTest : BearcatIntegrationTest
{
    private const string HosterClassName = "TestHoster";
    private const string CaptchaHosterClassName = "CaptchaHoster";
    private const string SerializedConfig = "{\"apiKey\":\"secret\"}";

    private BearcatDbContext dbContext = null!;
    private Mock<IHoster> hosterMock = null!;
    private Mock<IHosterWithCaptchaVerification> captchaHosterMock = null!;
    private Mock<IHosterConfig> hosterConfigMock = null!;
    private Mock<IHosterFactory> hosterFactoryMock = null!;
    private Mock<INotificationService> notificationServiceMock = null!;
    private HosterRegistrationService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        hosterConfigMock = new Mock<IHosterConfig>(MockBehavior.Strict);
        hosterMock = new Mock<IHoster>(MockBehavior.Strict);
        hosterMock.Setup(h => h.HasFixedParallelUploadLimit).Returns(false);
        captchaHosterMock = new Mock<IHosterWithCaptchaVerification>(MockBehavior.Strict);
        hosterFactoryMock = new Mock<IHosterFactory>(MockBehavior.Strict);
        notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

        hosterFactoryMock.Setup(f => f.GetByName(HosterClassName)).Returns(hosterMock.Object);

        service = new HosterRegistrationService(
            new HosterConfigurationRepository(dbContext, dbContext, hosterFactoryMock.Object),
            hosterFactoryMock.Object,
            new HosterCaptchaVerificationService(notificationServiceMock.Object),
            NoOpSecretProtector.Instance
        );
    }

    [Test]
    public async Task TryLoginAsync_CaptchaRequired_MarksRegistrationInactiveAndRequiresCaptcha()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync(isActive: true);
        hosterMock
            .Setup(h => h.DeserializeHosterConfig(SerializedConfig))
            .Returns(hosterConfigMock.Object);
        hosterMock
            .Setup(h => h.TryLoginAsync(hosterConfigMock.Object, CancellationToken.None))
            .ThrowsAsync(new CaptchaVerificationRequiredException("Captcha required", 400, 2));
        notificationServiceMock
            .Setup(n => n.CreateWarningAsync(It.IsAny<string>(), CancellationToken.None))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.TryLoginAsync(registration.Id, CancellationToken.None);

        // Assert
        var updatedRegistration = await dbContext.HosterRegistrations.SingleAsync();
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Captcha required");
        updatedRegistration.IsActive.ShouldBeFalse();
        updatedRegistration.RequiresCaptchaVerification.ShouldBeTrue();
        notificationServiceMock.Verify(
            n =>
                n.CreateWarningAsync(
                    It.Is<string>(message => message.Contains("Captcha required")),
                    CancellationToken.None
                ),
            Times.Once
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task RegisterHosterAsync_ValidHoster_PersistsRegistrationAndReturnsId()
    {
        // Arrange
        var configuration = new Dictionary<string, string> { ["apiKey"] = "secret" };
        hosterMock.Setup(h => h.SerializeHosterConfig(configuration)).Returns(SerializedConfig);

        // Act
        var result = await service.RegisterHosterAsync(
            "Primary hoster",
            true,
            configuration,
            HosterClassName,
            cancellationToken: CancellationToken.None
        );

        // Assert
        var registration = await dbContext.HosterRegistrations.SingleAsync();

        result.ShouldBeGreaterThan(0);
        registration.ShouldNotBeNull();
        registration.Id.ShouldBe(result);
        registration.Name.ShouldBe("Primary hoster");
        registration.IsActive.ShouldBeTrue();
        registration.HosterClassName.ShouldBe(HosterClassName);
        registration.SerializedConfig.ShouldBe(SerializedConfig);
        hosterFactoryMock.Verify(f => f.GetByName(HosterClassName), Times.Once);
        hosterMock.Verify(h => h.SerializeHosterConfig(configuration), Times.Once);
    }

    [Test]
    public async Task ToggleIsActiveAsync_RegistrationExists_TogglesIsActive()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync(isActive: true);

        // Act
        await service.ToggleIsActiveAsync(registration.Id, CancellationToken.None);

        // Assert
        var result = await dbContext.HosterRegistrations.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(registration.Id);
        result.IsActive.ShouldBeFalse();
    }

    [Test]
    public async Task UpdateRegistrationAsync_RegistrationExists_UpdatesNameAndSerializedConfig()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync(isActive: true);
        var configuration = new Dictionary<string, string> { ["apiKey"] = "updated" };
        hosterMock
            .Setup(h => h.DeserializeHosterConfig(SerializedConfig))
            .Returns(hosterConfigMock.Object);
        hosterConfigMock
            .Setup(c => c.ToDictionary())
            .Returns(new Dictionary<string, string> { ["apiKey"] = "secret" });
        hosterMock
            .Setup(h =>
                h.SerializeHosterConfig(
                    It.Is<Dictionary<string, string>>(config => config["apiKey"] == "updated")
                )
            )
            .Returns("{\"apiKey\":\"updated\"}");

        // Act
        await service.UpdateRegistrationAsync(
            registration.Id,
            "Updated hoster",
            configuration,
            cancellationToken: CancellationToken.None
        );

        // Assert
        var result = await dbContext.HosterRegistrations.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(registration.Id);
        result.Name.ShouldBe("Updated hoster");
        result.SerializedConfig.ShouldBe("{\"apiKey\":\"updated\"}");
        result.HosterClassName.ShouldBe(HosterClassName);
        hosterFactoryMock.Verify(f => f.GetByName(HosterClassName), Times.Once);
        hosterMock.Verify(
            h =>
                h.SerializeHosterConfig(
                    It.Is<Dictionary<string, string>>(config => config["apiKey"] == "updated")
                ),
            Times.Once
        );
    }

    [Test]
    public async Task TryLoginAsync_RegistrationExists_DelegatesToHoster()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync(isActive: true);
        var loginResult = new TryLoginResult(true, null);
        hosterMock
            .Setup(h => h.DeserializeHosterConfig(SerializedConfig))
            .Returns(hosterConfigMock.Object);
        hosterMock
            .Setup(h => h.TryLoginAsync(hosterConfigMock.Object, CancellationToken.None))
            .ReturnsAsync(loginResult);

        // Act
        var result = await service.TryLoginAsync(registration.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(loginResult);
        hosterFactoryMock.Verify(f => f.GetByName(HosterClassName), Times.Once);
        hosterMock.Verify(h => h.DeserializeHosterConfig(SerializedConfig), Times.Once);
        hosterMock.Verify(
            h => h.TryLoginAsync(hosterConfigMock.Object, CancellationToken.None),
            Times.Once
        );
    }

    [Test]
    public async Task RemoveAsync_RegistrationExists_RemovesRegistration()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync(isActive: true);

        // Act
        await service.RemoveAsync(registration.Id, CancellationToken.None);

        // Assert
        var result = await dbContext.HosterRegistrations.AnyAsync();

        result.ShouldBeFalse();
    }

    [Test]
    public async Task RegisterHosterAsync_HosterHasFixedParallelLimit_IgnoresOverride()
    {
        // Arrange
        var configuration = new Dictionary<string, string> { ["apiKey"] = "secret" };
        hosterMock.Setup(h => h.HasFixedParallelUploadLimit).Returns(true);
        hosterMock.Setup(h => h.SerializeHosterConfig(configuration)).Returns(SerializedConfig);

        // Act
        await service.RegisterHosterAsync(
            "Primary hoster",
            true,
            configuration,
            HosterClassName,
            maxParallelUploadsOverride: 5,
            cancellationToken: CancellationToken.None
        );

        // Assert
        var registration = await dbContext.HosterRegistrations.SingleAsync();
        registration.MaxParallelUploadsOverride.ShouldBeNull();
    }

    [Test]
    public async Task RegisterHosterAsync_NoFixedParallelLimit_PersistsOverride()
    {
        // Arrange
        var configuration = new Dictionary<string, string> { ["apiKey"] = "secret" };
        hosterMock.Setup(h => h.SerializeHosterConfig(configuration)).Returns(SerializedConfig);

        // Act
        await service.RegisterHosterAsync(
            "Primary hoster",
            true,
            configuration,
            HosterClassName,
            maxParallelUploadsOverride: 5,
            cancellationToken: CancellationToken.None
        );

        // Assert
        var registration = await dbContext.HosterRegistrations.SingleAsync();
        registration.MaxParallelUploadsOverride.ShouldBe(5);
    }

    [Test]
    public async Task TryLoginAsync_LoginSucceeds_ClearsCaptchaRequirement()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync(
            isActive: false,
            requiresCaptchaVerification: true
        );
        hosterMock
            .Setup(h => h.DeserializeHosterConfig(SerializedConfig))
            .Returns(hosterConfigMock.Object);
        hosterMock
            .Setup(h => h.TryLoginAsync(hosterConfigMock.Object, CancellationToken.None))
            .ReturnsAsync(new TryLoginResult(true, null));

        // Act
        var result = await service.TryLoginAsync(registration.Id, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var updated = await dbContext.HosterRegistrations.SingleAsync();
        updated.RequiresCaptchaVerification.ShouldBeFalse();
    }

    [Test]
    public async Task RequestCaptchaChallengeAsync_CaptchaHoster_DelegatesToHoster()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync(
            isActive: false,
            hosterClassName: CaptchaHosterClassName
        );
        var challengeResult = new CaptchaChallengeResult(
            IsSuccess: true,
            Challenge: "challenge",
            CaptchaUrl: "https://captcha.example/image.png"
        );
        SetupCaptchaHoster();
        captchaHosterMock
            .Setup(h =>
                h.RequestCaptchaChallengeAsync(hosterConfigMock.Object, CancellationToken.None)
            )
            .ReturnsAsync(challengeResult);

        // Act
        var result = await service.RequestCaptchaChallengeAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(challengeResult);
    }

    [Test]
    public async Task VerifyCaptchaAsync_VerificationSucceeds_ClearsCaptchaAndActivates()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync(
            isActive: false,
            hosterClassName: CaptchaHosterClassName,
            requiresCaptchaVerification: true
        );
        SetupCaptchaHoster();
        captchaHosterMock
            .Setup(h =>
                h.VerifyCaptchaAsync(
                    hosterConfigMock.Object,
                    "challenge",
                    "response",
                    CancellationToken.None
                )
            )
            .ReturnsAsync(new TryLoginResult(true, null));

        // Act
        var result = await service.VerifyCaptchaAsync(
            registration.Id,
            "challenge",
            "response",
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var updated = await dbContext.HosterRegistrations.SingleAsync();
        updated.RequiresCaptchaVerification.ShouldBeFalse();
        updated.IsActive.ShouldBeTrue();
    }

    [Test]
    public async Task VerifyCaptchaAsync_VerificationFails_KeepsCaptchaRequirement()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync(
            isActive: false,
            hosterClassName: CaptchaHosterClassName,
            requiresCaptchaVerification: true
        );
        SetupCaptchaHoster();
        captchaHosterMock
            .Setup(h =>
                h.VerifyCaptchaAsync(
                    hosterConfigMock.Object,
                    "challenge",
                    "wrong",
                    CancellationToken.None
                )
            )
            .ReturnsAsync(new TryLoginResult(false, "invalid"));

        // Act
        var result = await service.VerifyCaptchaAsync(
            registration.Id,
            "challenge",
            "wrong",
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeFalse();
        var updated = await dbContext.HosterRegistrations.SingleAsync();
        updated.RequiresCaptchaVerification.ShouldBeTrue();
        updated.IsActive.ShouldBeFalse();
    }

    [Test]
    public async Task RequestCaptchaChallengeAsync_HosterWithoutCaptchaSupport_Throws()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync(isActive: true);
        hosterMock.SetupGet(h => h.Name).Returns("TestHoster");

        // Act / Assert
        await Should.ThrowAsync<InvalidOperationException>(() =>
            service.RequestCaptchaChallengeAsync(registration.Id, CancellationToken.None)
        );
    }

    [Test]
    public async Task MarkCaptchaVerificationRequiredAsync_RegistrationExists_MarksAndNotifies()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync(isActive: true);
        notificationServiceMock
            .Setup(n => n.CreateWarningAsync(It.IsAny<string>(), CancellationToken.None))
            .Returns(Task.CompletedTask);

        // Act
        await service.MarkCaptchaVerificationRequiredAsync(
            registration.Id,
            "captcha please",
            CancellationToken.None
        );

        // Assert
        var updated = await dbContext.HosterRegistrations.SingleAsync();
        updated.RequiresCaptchaVerification.ShouldBeTrue();
        updated.IsActive.ShouldBeFalse();
        notificationServiceMock.Verify(
            n =>
                n.CreateWarningAsync(
                    It.Is<string>(message => message.Contains("captcha please")),
                    CancellationToken.None
                ),
            Times.Once
        );
    }

    private void SetupCaptchaHoster()
    {
        hosterFactoryMock
            .Setup(f => f.GetByName(CaptchaHosterClassName))
            .Returns(captchaHosterMock.Object);
        captchaHosterMock
            .Setup(h => h.DeserializeHosterConfig(SerializedConfig))
            .Returns(hosterConfigMock.Object);
    }

    private async Task<HosterRegistration> AddHosterRegistrationAsync(
        bool isActive,
        string hosterClassName = HosterClassName,
        bool requiresCaptchaVerification = false
    )
    {
        var registration = new HosterRegistration
        {
            Name = "Primary hoster",
            IsActive = isActive,
            HosterClassName = hosterClassName,
            SerializedConfig = SerializedConfig,
            RequiresCaptchaVerification = requiresCaptchaVerification,
        };

        dbContext.HosterRegistrations.Add(registration);
        await dbContext.SaveChangesAsync();

        return registration;
    }
}
