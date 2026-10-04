using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;
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
    public async Task GetAllAsync_NoTypeFilter_ReturnsAllTemplatesOrderedByName()
    {
        // Arrange
        var updatedAt = new DateTime(2026, 9, 1, 23, 59, 59, 999, DateTimeKind.Utc);
        var thirdTemplate = AddTemplate("Template c", ForumPostTemplateType.Release, updatedAt);
        var firstTemplate = AddTemplate(
            "Template a",
            ForumPostTemplateType.ReleaseCollection,
            updatedAt
        );
        var secondTemplate = AddTemplate("Template b", ForumPostTemplateType.Release, updatedAt);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetAllAsync(cancellationToken: CancellationToken.None);

        // Assert
        result.ShouldBe([
            new ForumPostTemplateSummaryReadModel(
                firstTemplate.Id,
                "Template a",
                ForumPostTemplateType.ReleaseCollection,
                ForumPostTemplateOutputFormat.BBCode,
                updatedAt,
                ForumPostingRuleCount: 0
            ),
            new ForumPostTemplateSummaryReadModel(
                secondTemplate.Id,
                "Template b",
                ForumPostTemplateType.Release,
                ForumPostTemplateOutputFormat.BBCode,
                updatedAt,
                ForumPostingRuleCount: 0
            ),
            new ForumPostTemplateSummaryReadModel(
                thirdTemplate.Id,
                "Template c",
                ForumPostTemplateType.Release,
                ForumPostTemplateOutputFormat.BBCode,
                updatedAt,
                ForumPostingRuleCount: 0
            ),
        ]);
    }

    [Test]
    public async Task GetAllAsync_TypeFilter_ReturnsOnlyTemplatesOfType()
    {
        // Arrange
        var updatedAt = new DateTime(2026, 9, 2, 0, 0, 0, 1, DateTimeKind.Utc);
        AddTemplate("Template a", ForumPostTemplateType.ReleaseCollection, updatedAt);
        var releaseTemplate = AddTemplate("Template b", ForumPostTemplateType.Release, updatedAt);
        var otherReleaseTemplate = AddTemplate(
            "Template c",
            ForumPostTemplateType.Release,
            updatedAt
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetAllAsync(
            ForumPostTemplateType.Release,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result
            .Select(template => template.ForumPostTemplateId)
            .ShouldBe([releaseTemplate.Id, otherReleaseTemplate.Id]);
        result.ShouldAllBe(template => template.Type == ForumPostTemplateType.Release);
    }

    [Test]
    public async Task GetAllAsync_OutputFormatFilter_ReturnsOnlyTemplatesOfOutputFormat()
    {
        // Arrange
        var updatedAt = new DateTime(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);
        AddTemplate(
            "Template a",
            ForumPostTemplateType.Release,
            updatedAt,
            ForumPostTemplateOutputFormat.BBCode
        );
        var plainTextTemplate = AddTemplate(
            "Template b",
            ForumPostTemplateType.Release,
            updatedAt,
            ForumPostTemplateOutputFormat.PlainText
        );
        var otherPlainTextTemplate = AddTemplate(
            "Template c",
            ForumPostTemplateType.ReleaseCollection,
            updatedAt,
            ForumPostTemplateOutputFormat.PlainText
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetAllAsync(
            outputFormat: ForumPostTemplateOutputFormat.PlainText,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result
            .Select(template => template.ForumPostTemplateId)
            .ShouldBe([plainTextTemplate.Id, otherPlainTextTemplate.Id]);
        result.ShouldAllBe(template =>
            template.OutputFormat == ForumPostTemplateOutputFormat.PlainText
        );
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

    [Test]
    public async Task GetDetailAsync_TemplateExists_ReturnsDetailWithOutputFormat()
    {
        // Arrange
        var updatedAt = new DateTime(2026, 9, 5, 10, 0, 0, DateTimeKind.Utc);
        var template = AddTemplate(
            "Template a",
            ForumPostTemplateType.ReleaseCollection,
            updatedAt,
            ForumPostTemplateOutputFormat.PlainText
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetDetailAsync(template.Id, CancellationToken.None);

        // Assert
        result.ShouldBe(
            new ForumPostTemplateDetailReadModel(
                template.Id,
                "Template a",
                ForumPostTemplateType.ReleaseCollection,
                ForumPostTemplateOutputFormat.PlainText,
                "Body"
            )
        );
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
