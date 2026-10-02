using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageForumPostingRules.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageForumPostingRules;

public class ForumPostingRuleRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private ForumPostingRuleRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        var readDbContext = CreateDbContext();
        readDbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        repository = new ForumPostingRuleRepository(readDbContext, CreateDbContext());
    }

    [Test]
    public async Task GetAllAsync_RulesOfSeveralRegistrations_ReturnsRulesOfRegistrationInSortOrder()
    {
        // Arrange
        var template = AddForumPostTemplate("Release post");
        var registration = AddRegistration("Forum A");
        var otherRegistration = AddRegistration("Forum B");
        var updatedAt = new DateTime(2026, 9, 1, 23, 59, 59, 999, DateTimeKind.Utc);
        var lastRule = AddRule(registration, template, "Rule C", sortOrder: 1);
        var firstRule = AddRule(
            registration,
            template,
            "Rule A",
            sortOrder: 0,
            threadPrefixId: "7",
            postMode: ForumPostPostMode.AlwaysNewThread,
            isEnabled: false,
            updatedAt: updatedAt
        );
        var tiedRule = AddRule(registration, template, "Rule B", sortOrder: 0);
        AddRule(otherRegistration, template, "Other rule", sortOrder: 0);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetAllAsync(registration.Id, CancellationToken.None);

        // Assert
        result
            .Select(rule => rule.ForumPostingRuleId)
            .ShouldBe([firstRule.Id, tiedRule.Id, lastRule.Id]);
        result[0]
            .ShouldBe(
                new ForumPostingRuleSummaryReadModel(
                    firstRule.Id,
                    registration.Id,
                    0,
                    "Rule A",
                    "12",
                    "Forum > Releases",
                    "7",
                    template.Id,
                    "Release post",
                    ForumPostPostMode.AlwaysNewThread,
                    false,
                    updatedAt
                )
            );
        result[1].ThreadPrefixId.ShouldBeNull();
        result[1].PostMode.ShouldBe(ForumPostPostMode.ReplyToExistingElseNewThread);
        result[1].IsEnabled.ShouldBeTrue();
    }

    [Test]
    public async Task GetDetailAsync_RuleExists_ReturnsRuleWithConditionJson()
    {
        // Arrange
        var template = AddForumPostTemplate("Release post");
        var registration = AddRegistration("Forum A");
        var rule = AddRule(
            registration,
            template,
            "Café rule",
            sortOrder: 3,
            conditionJson: "{\"title\":\"Café\"}",
            threadPrefixId: "7"
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetDetailAsync(rule.Id, CancellationToken.None);

        // Assert
        result.ShouldBe(
            new ForumPostingRuleDetailReadModel(
                rule.Id,
                registration.Id,
                3,
                "Café rule",
                "{\"title\":\"Café\"}",
                "12",
                "Forum > Releases",
                "7",
                template.Id,
                "Release post",
                ForumPostPostMode.ReplyToExistingElseNewThread,
                true
            )
        );
    }

    [Test]
    public async Task GetDetailAsync_RuleDoesNotExist_ReturnsNull()
    {
        // Arrange
        var template = AddForumPostTemplate("Release post");
        var registration = AddRegistration("Forum A");
        var rule = AddRule(registration, template, "Rule A", sortOrder: 0);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetDetailAsync(rule.Id + 1, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task GetRulesForMatchingAsync_RulesOfSeveralRegistrations_ReturnsAllRulesOfRegistrationInSortOrder()
    {
        // Arrange
        var template = AddForumPostTemplate("Release post");
        var registration = AddRegistration("Forum A");
        var otherRegistration = AddRegistration("Forum B");
        var lastRule = AddRule(registration, template, "Rule C", sortOrder: 5);
        var disabledRule = AddRule(
            registration,
            template,
            "Rule A",
            sortOrder: 0,
            isEnabled: false,
            conditionJson: "{\"title\":\"Café\"}"
        );
        var middleRule = AddRule(registration, template, "Rule B", sortOrder: 2);
        AddRule(otherRegistration, template, "Other rule", sortOrder: 1);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetRulesForMatchingAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.Select(rule => rule.Id).ShouldBe([disabledRule.Id, middleRule.Id, lastRule.Id]);
        result[0].IsEnabled.ShouldBeFalse();
        result[0].ConditionJson.ShouldBe("{\"title\":\"Café\"}");
        result[0].ForumPostTemplateId.ShouldBe(template.Id);
    }

    [Test]
    public async Task GetRecentReleasesForPreviewAsync_ReleasesWithCloseTimestamps_ReturnsNewestReleasesByCreatedAtThenId()
    {
        // Arrange
        var firstRelease = AddRelease(
            "Bearcat.Release.001",
            new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc)
        );
        var secondRelease = AddRelease(
            "Bearcat.Release.002",
            new DateTime(2026, 9, 1, 10, 0, 0, 500, DateTimeKind.Utc)
        );
        var thirdRelease = AddRelease(
            "Bearcat.Release.003",
            new DateTime(2026, 9, 1, 10, 0, 0, 500, DateTimeKind.Utc)
        );
        AddRelease(
            "Bearcat.Release.004",
            new DateTime(2026, 8, 31, 23, 59, 59, 999, DateTimeKind.Utc)
        );
        var midnightRelease = AddRelease(
            "Bearcat.Release.005",
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
        );
        secondRelease.Classification = new ReleaseClassification
        {
            Title = "Bearcat Release",
            ContentType = ReleaseContentType.Movie,
            ClassifiedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetRecentReleasesForPreviewAsync(4, CancellationToken.None);

        // Assert
        result
            .Select(release => release.Id)
            .ShouldBe([thirdRelease.Id, secondRelease.Id, firstRelease.Id, midnightRelease.Id]);
        result[1].Classification.ShouldNotBeNull();
        result[1].Classification!.Title.ShouldBe("Bearcat Release");
        result[0].Classification.ShouldBeNull();
        result[0].ReleaseGroup.Name.ShouldBe("Bearcat.Release.003 group");
    }

    [Test]
    public async Task GetByIdAsync_RuleChangedAndSaved_PersistsChanges()
    {
        // Arrange
        var template = AddForumPostTemplate("Release post");
        var registration = AddRegistration("Forum A");
        var rule = AddRule(registration, template, "Rule A", sortOrder: 0);
        await DbContext.SaveChangesAsync();

        // Act
        var trackedRule = await repository.GetByIdAsync(rule.Id, CancellationToken.None);
        trackedRule.Name = "Renamed rule";
        trackedRule.IsEnabled = false;
        await repository.SaveChangesAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.ForumPostingRules.SingleAsync();
        result.Name.ShouldBe("Renamed rule");
        result.IsEnabled.ShouldBeFalse();
    }

    [Test]
    public async Task GetByDistributionSiteRegistrationAsync_RulesOfSeveralRegistrations_ReturnsTrackedRulesOfRegistrationInSortOrder()
    {
        // Arrange
        var template = AddForumPostTemplate("Release post");
        var registration = AddRegistration("Forum A");
        var otherRegistration = AddRegistration("Forum B");
        var secondRule = AddRule(registration, template, "Rule B", sortOrder: 1);
        var firstRule = AddRule(registration, template, "Rule A", sortOrder: 0);
        AddRule(otherRegistration, template, "Other rule", sortOrder: 0);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetByDistributionSiteRegistrationAsync(
            registration.Id,
            CancellationToken.None
        );
        result[0].SortOrder = 1;
        result[1].SortOrder = 0;
        await repository.SaveChangesAsync(CancellationToken.None);

        // Assert
        result.Select(rule => rule.Id).ShouldBe([firstRule.Id, secondRule.Id]);
        DbContext.ChangeTracker.Clear();
        (
            await DbContext.ForumPostingRules.SingleAsync(rule => rule.Id == firstRule.Id)
        ).SortOrder.ShouldBe(1);
        (
            await DbContext.ForumPostingRules.SingleAsync(rule => rule.Id == secondRule.Id)
        ).SortOrder.ShouldBe(0);
    }

    [Test]
    public async Task GetNextSortOrderAsync_RegistrationWithoutRules_ReturnsZero()
    {
        // Arrange
        var template = AddForumPostTemplate("Release post");
        var registration = AddRegistration("Forum A");
        var otherRegistration = AddRegistration("Forum B");
        AddRule(otherRegistration, template, "Other rule", sortOrder: 9);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetNextSortOrderAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0);
    }

    [Test]
    public async Task GetNextSortOrderAsync_RegistrationWithRules_ReturnsHighestSortOrderPlusOne()
    {
        // Arrange
        var template = AddForumPostTemplate("Release post");
        var registration = AddRegistration("Forum A");
        var otherRegistration = AddRegistration("Forum B");
        AddRule(registration, template, "Rule A", sortOrder: 0);
        AddRule(registration, template, "Rule B", sortOrder: 4);
        AddRule(registration, template, "Rule C", sortOrder: 2);
        AddRule(otherRegistration, template, "Other rule", sortOrder: 9);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetNextSortOrderAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(5);
    }

    [Test]
    public async Task GetForumPostTemplateTypeAsync_TemplateExists_ReturnsTemplateType()
    {
        // Arrange
        AddForumPostTemplate("Release post");
        var collectionTemplate = AddForumPostTemplate(
            "Collection post",
            ForumPostTemplateType.ReleaseCollection
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetForumPostTemplateTypeAsync(
            collectionTemplate.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(ForumPostTemplateType.ReleaseCollection);
    }

    [Test]
    public async Task GetForumPostTemplateTypeAsync_TemplateDoesNotExist_ReturnsNull()
    {
        // Arrange
        var template = AddForumPostTemplate("Release post");
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetForumPostTemplateTypeAsync(
            template.Id + 1,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task Add_NewRule_PersistsRule()
    {
        // Arrange
        var template = AddForumPostTemplate("Release post");
        var registration = AddRegistration("Forum A");
        await DbContext.SaveChangesAsync();
        var updatedAt = new DateTime(2026, 9, 2, 0, 0, 0, 1, DateTimeKind.Utc);

        // Act
        repository.Add(
            new ForumPostingRule
            {
                DistributionSiteRegistrationId = registration.Id,
                SortOrder = 0,
                Name = "Café rule",
                ConditionJson = "{\"title\":\"Café\"}",
                TargetNodeId = "12",
                TargetPathSnapshot = "Forum > Releases",
                ThreadPrefixId = "7",
                ForumPostTemplateId = template.Id,
                PostMode = ForumPostPostMode.AlwaysNewThread,
                IsEnabled = true,
                CreatedAt = updatedAt,
                UpdatedAt = updatedAt,
            }
        );
        await repository.SaveChangesAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.ForumPostingRules.SingleAsync();
        result.DistributionSiteRegistrationId.ShouldBe(registration.Id);
        result.Name.ShouldBe("Café rule");
        result.ConditionJson.ShouldBe("{\"title\":\"Café\"}");
        result.ThreadPrefixId.ShouldBe("7");
        result.PostMode.ShouldBe(ForumPostPostMode.AlwaysNewThread);
        result.CreatedAt.ShouldBe(updatedAt);
        result.UpdatedAt.ShouldBe(updatedAt);
    }

    [Test]
    public async Task Remove_RuleExists_DeletesOnlyThatRule()
    {
        // Arrange
        var template = AddForumPostTemplate("Release post");
        var registration = AddRegistration("Forum A");
        var rule = AddRule(registration, template, "Rule A", sortOrder: 0);
        var otherRule = AddRule(registration, template, "Rule B", sortOrder: 1);
        await DbContext.SaveChangesAsync();

        // Act
        repository.Remove(await repository.GetByIdAsync(rule.Id, CancellationToken.None));
        await repository.SaveChangesAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        (
            await DbContext.ForumPostingRules.Select(candidate => candidate.Id).ToListAsync()
        ).ShouldBe([otherRule.Id]);
        (await DbContext.DistributionSiteRegistrations.CountAsync()).ShouldBe(1);
        (await DbContext.ForumPostTemplates.CountAsync()).ShouldBe(1);
    }

    private ForumPostTemplate AddForumPostTemplate(
        string name,
        ForumPostTemplateType type = ForumPostTemplateType.Release
    )
    {
        var template = new ForumPostTemplate
        {
            Name = name,
            Type = type,
            TemplateBody = string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        DbContext.ForumPostTemplates.Add(template);

        return template;
    }

    private DistributionSiteRegistration AddRegistration(string name)
    {
        var registration = new DistributionSiteRegistration
        {
            Name = name,
            DistributionSiteClassName = "TestForum",
            SerializedConfig = "{}",
            IsActive = true,
        };

        DbContext.DistributionSiteRegistrations.Add(registration);

        return registration;
    }

    private ForumPostingRule AddRule(
        DistributionSiteRegistration registration,
        ForumPostTemplate template,
        string name,
        int sortOrder,
        string conditionJson = "{}",
        string? threadPrefixId = null,
        ForumPostPostMode postMode = ForumPostPostMode.ReplyToExistingElseNewThread,
        bool isEnabled = true,
        DateTime? updatedAt = null
    )
    {
        var rule = new ForumPostingRule
        {
            DistributionSiteRegistration = registration,
            SortOrder = sortOrder,
            Name = name,
            ConditionJson = conditionJson,
            TargetNodeId = "12",
            TargetPathSnapshot = "Forum > Releases",
            ThreadPrefixId = threadPrefixId,
            ForumPostTemplate = template,
            PostMode = postMode,
            IsEnabled = isEnabled,
            CreatedAt = updatedAt ?? DateTime.UtcNow,
            UpdatedAt = updatedAt ?? DateTime.UtcNow,
        };

        DbContext.ForumPostingRules.Add(rule);

        return rule;
    }

    private Release AddRelease(string name, DateTime createdAt)
    {
        var release = new Release
        {
            Name = name,
            CreatedAt = createdAt,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/{name}",
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{name} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };

        DbContext.Releases.Add(release);

        return release;
    }
}
