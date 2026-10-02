using Bearcat.Abstractions.DistributionSite;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageDistributionSites.ReadModels;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageDistributionSites;

public class DistributionSiteRegistrationReadRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string ForumClassName = "TestForum";
    private const string BlogClassName = "TestBlog";

    private DistributionSiteRegistrationReadRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        var readDbContext = CreateDbContext();
        readDbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        var distributionSiteFactoryMock = new Mock<IDistributionSiteFactory>(MockBehavior.Strict);
        distributionSiteFactoryMock
            .Setup(factory => factory.GetDistributionSites())
            .Returns([
                new DistributionSiteDto(
                    "Test forum",
                    ForumClassName,
                    DistributionSiteKind.Forum,
                    []
                ),
                new DistributionSiteDto("Test blog", BlogClassName, DistributionSiteKind.Blog, []),
            ]);

        repository = new DistributionSiteRegistrationReadRepository(
            readDbContext,
            distributionSiteFactoryMock.Object
        );
    }

    [Test]
    public async Task GetAllAsync_SeveralRegistrations_ReturnsRegistrationsOrderedByNameWithRuleCounts()
    {
        // Arrange
        var template = AddForumPostTemplate();
        var secondForum = AddRegistration(
            "Forum b",
            ForumClassName,
            isActive: false,
            enableAutomaticPosting: true
        );
        AddRule(secondForum, template, sortOrder: 0);
        AddRule(secondForum, template, sortOrder: 1);
        var blog = AddRegistration(
            "Blog",
            BlogClassName,
            hasUnreadableSecrets: true,
            stripDotsForThreadSearch: false
        );
        var firstForum = AddRegistration("Forum a", ForumClassName);
        AddRule(firstForum, template, sortOrder: 0);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetAllAsync(CancellationToken.None);

        // Assert
        result.ShouldBe([
            new DistributionSiteRegistrationReadModel(
                DistributionSiteRegistrationId: blog.Id,
                Name: "Blog",
                DistributionSiteClassName: BlogClassName,
                DistributionSiteName: "Test blog",
                Kind: DistributionSiteKind.Blog,
                IsActive: true,
                HasUnreadableSecrets: true,
                EnableAutomaticPosting: false,
                StripDotsForThreadSearch: false,
                PostingRuleCount: 0
            ),
            new DistributionSiteRegistrationReadModel(
                DistributionSiteRegistrationId: firstForum.Id,
                Name: "Forum a",
                DistributionSiteClassName: ForumClassName,
                DistributionSiteName: "Test forum",
                Kind: DistributionSiteKind.Forum,
                IsActive: true,
                HasUnreadableSecrets: false,
                EnableAutomaticPosting: false,
                StripDotsForThreadSearch: true,
                PostingRuleCount: 1
            ),
            new DistributionSiteRegistrationReadModel(
                DistributionSiteRegistrationId: secondForum.Id,
                Name: "Forum b",
                DistributionSiteClassName: ForumClassName,
                DistributionSiteName: "Test forum",
                Kind: DistributionSiteKind.Forum,
                IsActive: false,
                HasUnreadableSecrets: false,
                EnableAutomaticPosting: true,
                StripDotsForThreadSearch: true,
                PostingRuleCount: 2
            ),
        ]);
    }

    [Test]
    public async Task GetByIdAsync_RegistrationExists_ReturnsRegistrationWithRuleCount()
    {
        // Arrange
        var template = AddForumPostTemplate();
        var registration = AddRegistration(
            "Forum a",
            ForumClassName,
            enableAutomaticPosting: true,
            stripDotsForThreadSearch: false
        );
        AddRule(registration, template, sortOrder: 0);
        AddRule(registration, template, sortOrder: 1);
        var otherRegistration = AddRegistration("Forum b", ForumClassName);
        AddRule(otherRegistration, template, sortOrder: 0);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetByIdAsync(registration.Id, CancellationToken.None);

        // Assert
        result.ShouldBe(
            new DistributionSiteRegistrationReadModel(
                DistributionSiteRegistrationId: registration.Id,
                Name: "Forum a",
                DistributionSiteClassName: ForumClassName,
                DistributionSiteName: "Test forum",
                Kind: DistributionSiteKind.Forum,
                IsActive: true,
                HasUnreadableSecrets: false,
                EnableAutomaticPosting: true,
                StripDotsForThreadSearch: false,
                PostingRuleCount: 2
            )
        );
    }

    [Test]
    public async Task GetByIdAsync_RegistrationDoesNotExist_ReturnsNull()
    {
        // Arrange
        var registration = AddRegistration("Forum a", ForumClassName);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetByIdAsync(registration.Id + 1, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    private ForumPostTemplate AddForumPostTemplate()
    {
        var template = new ForumPostTemplate
        {
            Name = "Release post",
            TemplateBody = string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        DbContext.ForumPostTemplates.Add(template);

        return template;
    }

    private DistributionSiteRegistration AddRegistration(
        string name,
        string className,
        bool isActive = true,
        bool hasUnreadableSecrets = false,
        bool enableAutomaticPosting = false,
        bool stripDotsForThreadSearch = true
    )
    {
        var registration = new DistributionSiteRegistration
        {
            Name = name,
            DistributionSiteClassName = className,
            SerializedConfig = "{}",
            IsActive = isActive,
            HasUnreadableSecrets = hasUnreadableSecrets,
            EnableAutomaticPosting = enableAutomaticPosting,
            StripDotsForThreadSearch = stripDotsForThreadSearch,
        };

        DbContext.DistributionSiteRegistrations.Add(registration);

        return registration;
    }

    private void AddRule(
        DistributionSiteRegistration registration,
        ForumPostTemplate template,
        int sortOrder
    )
    {
        DbContext.ForumPostingRules.Add(
            new ForumPostingRule
            {
                DistributionSiteRegistration = registration,
                SortOrder = sortOrder,
                Name = $"Rule {sortOrder}",
                ConditionJson = "{}",
                TargetNodeId = "1",
                TargetPathSnapshot = "Forum",
                ForumPostTemplate = template,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }
        );
    }
}
