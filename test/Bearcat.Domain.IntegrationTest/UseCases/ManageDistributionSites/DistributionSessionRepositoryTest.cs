using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Domain.Entities;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.DistributionSites;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageDistributionSites;

public class DistributionSessionRepositoryTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private DistributionSessionRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        repository = new DistributionSessionRepository(dbContext, NoOpSecretProtector.Instance);
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task GetByRegistrationIdAsync_SavedSession_ReturnsSessionWithBaseUrl()
    {
        // Arrange
        var registration = await AddRegistrationAsync(encryptedSession: null);
        var session = new DistributionSession(
            BaseUrl: "https://example.org/community/",
            UserAgent: "Bearcat",
            Cookies:
            [
                new SessionCookie(
                    Name: "xf_user",
                    Value: "value",
                    Domain: "example.org",
                    Path: "/community/"
                ),
            ]
        );
        await repository.SaveAsync(registration.Id, session, CancellationToken.None);

        // Act
        var result = await repository.GetByRegistrationIdAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.BaseUrl.ShouldBe("https://example.org/community/");
        result.UserAgent.ShouldBe("Bearcat");
        result.Cookies.ShouldBe(session.Cookies);
    }

    [Test]
    public async Task GetByRegistrationIdAsync_SessionSavedWithoutBaseUrl_ReturnsNull()
    {
        // Arrange
        var registration = await AddRegistrationAsync(
            encryptedSession: """{"UserAgent":"Bearcat","Cookies":[]}"""
        );

        // Act
        var result = await repository.GetByRegistrationIdAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeNull();
    }

    private async Task<DistributionSiteRegistration> AddRegistrationAsync(string? encryptedSession)
    {
        var registration = new DistributionSiteRegistration
        {
            Name = "Forum",
            DistributionSiteClassName = "TestForum",
            SerializedConfig = "{}",
            EncryptedSession = encryptedSession,
            IsActive = true,
        };

        dbContext.DistributionSiteRegistrations.Add(registration);
        await dbContext.SaveChangesAsync();

        return registration;
    }
}
