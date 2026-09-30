using Bearcat.Abstractions.DistributionSite;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Abstractions.DistributionSite.Results;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageDistributionSites;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.DistributionSites;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageDistributionSites;

public class DistributionSiteSessionServiceTest : BearcatIntegrationTest
{
    private const string DistributionSiteClassName = "TestForum";
    private const string StoredSerializedConfig = "stored-config";

    private BearcatDbContext dbContext = null!;
    private Mock<IForumDistributionSite> distributionSiteMock = null!;
    private IDistributionSiteConfig storedConfig = null!;
    private DistributionSiteSessionService sessionService = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        storedConfig = Mock.Of<IDistributionSiteConfig>();
        distributionSiteMock = new Mock<IForumDistributionSite>(MockBehavior.Strict);
        distributionSiteMock
            .Setup(site => site.DeserializeConfig(StoredSerializedConfig))
            .Returns(storedConfig);
        var distributionSiteFactoryMock = new Mock<IDistributionSiteFactory>(MockBehavior.Strict);
        distributionSiteFactoryMock
            .Setup(factory => factory.GetByClassName(DistributionSiteClassName))
            .Returns(distributionSiteMock.Object);

        sessionService = new DistributionSiteSessionService(
            new DistributionSiteRegistrationWriteRepository(dbContext),
            new DistributionSessionRepository(dbContext, NoOpSecretProtector.Instance),
            distributionSiteFactoryMock.Object,
            NoOpSecretProtector.Instance
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task TestLoginAsync_LoginRejected_ReturnsErrorMessageOfSite()
    {
        // Arrange
        var registration = await AddRegistrationAsync();
        SetupLogInResult(
            new DistributionSiteLoginResult(
                Session: null,
                ErrorMessage: "Incorrect password. Please try again."
            )
        );

        // Act
        var result = await sessionService.TestLoginAsync(registration.Id, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Incorrect password. Please try again.");
        dbContext.ChangeTracker.Clear();
        (
            await dbContext.DistributionSiteRegistrations.SingleAsync()
        ).EncryptedSession.ShouldBeNull();
    }

    [Test]
    public async Task TestLoginAsync_LoginAccepted_StoresSession()
    {
        // Arrange
        var registration = await AddRegistrationAsync();
        var session = new DistributionSession(
            BaseUrl: "https://example.org/",
            UserAgent: "Bearcat",
            Cookies:
            [
                new SessionCookie(
                    Name: "xf_user",
                    Value: "value",
                    Domain: "example.org",
                    Path: "/"
                ),
            ]
        );
        SetupLogInResult(new DistributionSiteLoginResult(Session: session, ErrorMessage: null));

        // Act
        var result = await sessionService.TestLoginAsync(registration.Id, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        dbContext.ChangeTracker.Clear();
        var storedSession = await new DistributionSessionRepository(
            dbContext,
            NoOpSecretProtector.Instance
        ).GetByRegistrationIdAsync(registration.Id, CancellationToken.None);
        storedSession.ShouldNotBeNull();
        storedSession.BaseUrl.ShouldBe("https://example.org/");
        storedSession.Cookies.ShouldBe(session.Cookies);
    }

    [Test]
    public async Task GetTargetHierarchyAsync_NoStoredSessionAndLoginRejected_ThrowsWithErrorMessageOfSite()
    {
        // Arrange
        var registration = await AddRegistrationAsync();
        SetupLogInResult(
            new DistributionSiteLoginResult(
                Session: null,
                ErrorMessage: "The forum requires a captcha for login, which Bearcat cannot solve."
            )
        );

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            sessionService.GetTargetHierarchyAsync(registration.Id, CancellationToken.None)
        );

        // Assert
        exception.Message.ShouldBe(
            "Login to distribution site 'Forum' failed: The forum requires a captcha for login, which Bearcat cannot solve."
        );
        distributionSiteMock.Verify(
            site => site.LogInAsync(storedConfig, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    private void SetupLogInResult(DistributionSiteLoginResult loginResult)
    {
        distributionSiteMock
            .Setup(site => site.LogInAsync(storedConfig, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loginResult);
    }

    private async Task<DistributionSiteRegistration> AddRegistrationAsync()
    {
        var registration = new DistributionSiteRegistration
        {
            Name = "Forum",
            DistributionSiteClassName = DistributionSiteClassName,
            SerializedConfig = StoredSerializedConfig,
            IsActive = true,
        };

        dbContext.DistributionSiteRegistrations.Add(registration);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        return registration;
    }
}
