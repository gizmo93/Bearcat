using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ForumPostTemplateRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private ForumPostTemplateRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        var readDbContext = CreateDbContext();
        readDbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        repository = new ForumPostTemplateRepository(readDbContext, CreateDbContext());
    }

    [Test]
    public async Task GetAllAsync_TypeAndOutputFormatFilter_ReturnsOnlyTemplatesMatchingBoth()
    {
        // Arrange
        var updatedAt = new DateTime(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);
        AddTemplate(
            "Template a",
            ForumPostTemplateType.Release,
            updatedAt,
            ForumPostTemplateOutputFormat.PlainText
        );
        var matchingTemplate = AddTemplate(
            "Template b",
            ForumPostTemplateType.Release,
            updatedAt,
            ForumPostTemplateOutputFormat.BBCode
        );
        AddTemplate(
            "Template c",
            ForumPostTemplateType.ReleaseCollection,
            updatedAt,
            ForumPostTemplateOutputFormat.BBCode
        );
        AddTemplate(
            "Template d",
            ForumPostTemplateType.ReleaseCollection,
            updatedAt,
            ForumPostTemplateOutputFormat.PlainText
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetAllAsync(
            ForumPostTemplateType.Release,
            ForumPostTemplateOutputFormat.BBCode,
            CancellationToken.None
        );

        // Assert
        result.Select(template => template.ForumPostTemplateId).ShouldBe([matchingTemplate.Id]);
    }

    [Test]
    public async Task GetAllAsync_TemplatesReferencedByPostingRules_ReturnsForumPostingRuleCountPerTemplate()
    {
        // Arrange
        var updatedAt = new DateTime(2026, 9, 4, 8, 30, 0, DateTimeKind.Utc);
        var templateWithTwoRules = AddTemplate(
            "Template a",
            ForumPostTemplateType.Release,
            updatedAt
        );
        var templateWithOneRule = AddTemplate(
            "Template b",
            ForumPostTemplateType.Release,
            updatedAt
        );
        var templateWithoutRules = AddTemplate(
            "Template c",
            ForumPostTemplateType.Release,
            updatedAt
        );
        var registration = new DistributionSiteRegistration
        {
            Name = "Forum A",
            DistributionSiteClassName = "TestForum",
            SerializedConfig = "{}",
            IsActive = true,
        };
        AddForumPostingRule(registration, "Rule a", sortOrder: 0, templateWithTwoRules);
        AddForumPostingRule(registration, "Rule b", sortOrder: 1, templateWithOneRule);
        AddForumPostingRule(registration, "Rule c", sortOrder: 2, templateWithTwoRules);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetAllAsync(cancellationToken: CancellationToken.None);

        // Assert
        result
            .Select(template => (template.ForumPostTemplateId, template.ForumPostingRuleCount))
            .ShouldBe([
                (templateWithTwoRules.Id, 2),
                (templateWithOneRule.Id, 1),
                (templateWithoutRules.Id, 0),
            ]);
    }

    private void AddForumPostingRule(
        DistributionSiteRegistration registration,
        string name,
        int sortOrder,
        ForumPostTemplate template
    )
    {
        DbContext.ForumPostingRules.Add(
            new ForumPostingRule
            {
                DistributionSiteRegistration = registration,
                SortOrder = sortOrder,
                Name = name,
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

    private ForumPostTemplate AddTemplate(
        string name,
        ForumPostTemplateType type,
        DateTime updatedAt,
        ForumPostTemplateOutputFormat outputFormat = ForumPostTemplateOutputFormat.BBCode
    )
    {
        var template = new ForumPostTemplate
        {
            Name = name,
            Type = type,
            OutputFormat = outputFormat,
            TemplateBody = "Body",
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt,
        };

        DbContext.ForumPostTemplates.Add(template);

        return template;
    }
}
