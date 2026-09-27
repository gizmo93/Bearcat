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

        registrationService = new DistributionSiteRegistrationService(
            new DistributionSiteRegistrationWriteRepository(dbContext),
            new DistributionSiteRegistrationReadRepository(
                dbContext,
                distributionSiteFactoryMock.Object
            ),
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

    [Test]
    public async Task GetForumPostingRuleCountAsync_RulesOfSeveralRegistrations_CountsOnlyRulesOfRegistration()
    {
        // Arrange
        var registration = CreateRegistration("Primary forum");
        var otherRegistration = CreateRegistration("Other forum");
        var forumPostTemplate = new ForumPostTemplate
        {
            Name = "Release post",
            TemplateBody = string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        dbContext.ForumPostingRules.AddRange(
            CreateRule(registration, forumPostTemplate, sortOrder: 0),
            CreateRule(registration, forumPostTemplate, sortOrder: 1),
            CreateRule(otherRegistration, forumPostTemplate, sortOrder: 0)
        );
        await dbContext.SaveChangesAsync();

        // Act
        var result = await registrationService.GetForumPostingRuleCountAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(2);
    }

    [Test]
    public async Task GetForumPostingRuleCountAsync_RegistrationWithoutRules_ReturnsZero()
    {
        // Arrange
        var registration = CreateRegistration("Primary forum");
        dbContext.DistributionSiteRegistrations.Add(registration);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await registrationService.GetForumPostingRuleCountAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0);
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

    private static DistributionSiteRegistration CreateRegistration(string name)
    {
        return new DistributionSiteRegistration
        {
            Name = name,
            DistributionSiteClassName = "TestDistributionSite",
            SerializedConfig = "{}",
            IsActive = true,
        };
    }

    private static ForumPostingRule CreateRule(
        DistributionSiteRegistration registration,
        ForumPostTemplate forumPostTemplate,
        int sortOrder
    )
    {
        return new ForumPostingRule
        {
            DistributionSiteRegistration = registration,
            SortOrder = sortOrder,
            Name = $"Rule {sortOrder}",
            ConditionJson = "{}",
            TargetNodeId = "1",
            TargetPathSnapshot = "Forum",
            ForumPostTemplate = forumPostTemplate,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }
}
