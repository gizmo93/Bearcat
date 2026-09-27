using Bearcat.Abstractions.DistributionSite;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageDistributionSites;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageDistributionSites;

public class DistributionSiteRegistrationServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private DistributionSiteRegistrationService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        var distributionSiteFactoryMock = new Mock<IDistributionSiteFactory>(MockBehavior.Strict);

        service = new DistributionSiteRegistrationService(
            new DistributionSiteRegistrationWriteRepository(dbContext),
            new DistributionSiteRegistrationReadRepository(
                dbContext,
                distributionSiteFactoryMock.Object
            ),
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
        var result = await service.GetForumPostingRuleCountAsync(
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
        var result = await service.GetForumPostingRuleCountAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0);
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
