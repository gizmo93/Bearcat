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
                updatedAt
            ),
            new ForumPostTemplateSummaryReadModel(
                secondTemplate.Id,
                "Template b",
                ForumPostTemplateType.Release,
                updatedAt
            ),
            new ForumPostTemplateSummaryReadModel(
                thirdTemplate.Id,
                "Template c",
                ForumPostTemplateType.Release,
                updatedAt
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
            CancellationToken.None
        );

        // Assert
        result
            .Select(template => template.ForumPostTemplateId)
            .ShouldBe([releaseTemplate.Id, otherReleaseTemplate.Id]);
        result.ShouldAllBe(template => template.Type == ForumPostTemplateType.Release);
    }

    private ForumPostTemplate AddTemplate(
        string name,
        ForumPostTemplateType type,
        DateTime updatedAt
    )
    {
        var template = new ForumPostTemplate
        {
            Name = name,
            Type = type,
            TemplateBody = "Body",
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt,
        };

        DbContext.ForumPostTemplates.Add(template);

        return template;
    }
}
