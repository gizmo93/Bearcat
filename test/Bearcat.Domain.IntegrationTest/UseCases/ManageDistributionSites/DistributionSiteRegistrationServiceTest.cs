using Bearcat.Abstractions.DistributionSite;
using Bearcat.Domain.Entities;
using Bearcat.Domain.IntegrationTest.Shared.UnreadableSecrets;
using Bearcat.Domain.UseCases.ManageDistributionSites;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageDistributionSites;

public class DistributionSiteRegistrationServiceTest : BearcatIntegrationTest
{
    private const string DistributionSiteClassName = "TestForum";

    private BearcatDbContext dbContext = null!;
    private Mock<IDistributionSite> distributionSiteMock = null!;
    private Mock<IDistributionSiteFactory> distributionSiteFactoryMock = null!;
    private DistributionSiteRegistrationService registrationService = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        distributionSiteMock = new Mock<IDistributionSite>(MockBehavior.Strict);
        distributionSiteFactoryMock = new Mock<IDistributionSiteFactory>(MockBehavior.Strict);
        distributionSiteFactoryMock
            .Setup(factory => factory.Get(DistributionSiteClassName))
            .Returns(distributionSiteMock.Object);

        var repository = new DistributionSiteRegistrationWriteRepository(dbContext);

        registrationService = new DistributionSiteRegistrationService(
            repository,
            distributionSiteFactoryMock.Object,
            NoOpSecretProtector.Instance,
            UnreadableSecretsNotificationServiceFactory.Create(
                dbContext,
                CreateNotificationConfigurationProvider()
            )
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task UpdateAsync_RegistrationHasUnreadableSecrets_ReplacesConfigAndClearsFlag()
    {
        // Arrange
        var registration = await AddRegistrationWithUnreadableSecretsAsync();
        distributionSiteMock
            .Setup(site =>
                site.SerializeConfig(
                    It.Is<Dictionary<string, string>>(config =>
                        config.Count == 1 && config["Password"] == "new-password"
                    )
                )
            )
            .Returns("{\"Password\":\"new-password\"}");

        // Act
        await registrationService.UpdateAsync(
            registration.Id,
            "Forum",
            new Dictionary<string, string> { ["Password"] = "new-password" },
            CancellationToken.None
        );

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.DistributionSiteRegistrations.SingleAsync();

        result.SerializedConfig.ShouldBe("{\"Password\":\"new-password\"}");
        result.HasUnreadableSecrets.ShouldBeFalse();
        distributionSiteMock.Verify(
            site => site.DeserializeConfig(It.IsAny<string>()),
            Times.Never
        );
    }

    private async Task<DistributionSiteRegistration> AddRegistrationWithUnreadableSecretsAsync()
    {
        var registration = new DistributionSiteRegistration
        {
            Name = "Forum",
            DistributionSiteClassName = DistributionSiteClassName,
            SerializedConfig = "unreadable",
            IsActive = true,
            HasUnreadableSecrets = true,
        };

        dbContext.DistributionSiteRegistrations.Add(registration);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        return registration;
    }
}
