using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageForumPostTemplates;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageForumPostTemplates;

public class ForumPostTemplateServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private ForumPostTemplateService service = null!;

    [SetUp]
    public void Setup()
    {
        service = new ForumPostTemplateService(
            new ForumPostTemplateRepository(DbContext, DbContext)
        );
    }

    [Test]
    public async Task CreateAsync_ValidTemplate_PersistsTrimmedTemplateAndReturnsId()
    {
        // Act
        var id = await service.CreateAsync(
            "  Release template  ",
            ForumPostTemplateType.Release,
            "Body {{ release.name }}",
            CancellationToken.None
        );

        // Assert
        var template = await DbContext.ForumPostTemplates.SingleAsync();
        id.ShouldBe(template.Id);
        template.Name.ShouldBe("Release template");
        template.Type.ShouldBe(ForumPostTemplateType.Release);
        template.TemplateBody.ShouldBe("Body {{ release.name }}");
        template.CreatedAt.ShouldBe(template.UpdatedAt);
    }

    [Test]
    public async Task CreateAsync_NullTemplateBody_PersistsEmptyBody()
    {
        // Act
        var id = await service.CreateAsync(
            "Empty",
            ForumPostTemplateType.ReleaseCollection,
            templateBody: null,
            CancellationToken.None
        );

        // Assert
        var template = await DbContext.ForumPostTemplates.SingleAsync(t => t.Id == id);
        template.TemplateBody.ShouldBe(string.Empty);
    }

    [Test]
    public async Task UpdateAsync_TemplateExists_UpdatesAllFields()
    {
        // Arrange
        var id = await service.CreateAsync(
            "Original",
            ForumPostTemplateType.Release,
            "Original body",
            CancellationToken.None
        );

        // Act
        await service.UpdateAsync(
            id,
            "  Updated  ",
            ForumPostTemplateType.ReleaseCollection,
            "Updated body",
            CancellationToken.None
        );

        // Assert
        var template = await DbContext.ForumPostTemplates.SingleAsync(t => t.Id == id);
        template.Name.ShouldBe("Updated");
        template.Type.ShouldBe(ForumPostTemplateType.ReleaseCollection);
        template.TemplateBody.ShouldBe("Updated body");
    }

    [Test]
    public async Task UpdateAsync_NullTemplateBody_PersistsEmptyBody()
    {
        // Arrange
        var id = await service.CreateAsync(
            "Original",
            ForumPostTemplateType.Release,
            "Original body",
            CancellationToken.None
        );

        // Act
        await service.UpdateAsync(
            id,
            "Original",
            ForumPostTemplateType.Release,
            templateBody: null,
            CancellationToken.None
        );

        // Assert
        var template = await DbContext.ForumPostTemplates.SingleAsync(t => t.Id == id);
        template.TemplateBody.ShouldBe(string.Empty);
    }

    [Test]
    public async Task DeleteAsync_TemplateExists_RemovesTemplate()
    {
        // Arrange
        var template = new ForumPostTemplate
        {
            Name = "To delete",
            Type = ForumPostTemplateType.Release,
            TemplateBody = string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        DbContext.ForumPostTemplates.Add(template);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        await service.DeleteAsync(template.Id, CancellationToken.None);

        // Assert
        (await DbContext.ForumPostTemplates.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task DeleteAsync_TemplateUsedByPostedLocation_ClearsTemplateOfPostedLocation()
    {
        // Arrange
        var template = CreateTemplate("To delete");
        var postedLocation = new PostedLocation
        {
            Release = CreateRelease("Bearcat.Release.001"),
            ForumPostTemplate = template,
            Url = "https://forum.example/threads/1",
            CreatedAt = DateTime.UtcNow,
        };
        DbContext.PostedLocations.Add(postedLocation);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        await service.DeleteAsync(template.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        (await DbContext.ForumPostTemplates.AnyAsync()).ShouldBeFalse();
        var result = await DbContext.PostedLocations.SingleAsync();
        result.Id.ShouldBe(postedLocation.Id);
        result.ForumPostTemplateId.ShouldBeNull();
    }

    [Test]
    public async Task DeleteAsync_TemplateUsedByPostingRule_ThrowsAndKeepsTemplate()
    {
        // Arrange
        var template = CreateTemplate("To delete");
        DbContext.ForumPostingRules.Add(
            new ForumPostingRule
            {
                DistributionSiteRegistration = new DistributionSiteRegistration
                {
                    Name = "Forum A",
                    DistributionSiteClassName = "TestForum",
                    SerializedConfig = "{}",
                    IsActive = true,
                },
                SortOrder = 0,
                Name = "Rule A",
                ConditionJson = "{}",
                TargetNodeId = "1",
                TargetPathSnapshot = "Forum",
                ForumPostTemplate = template,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var action = () => service.DeleteAsync(template.Id, CancellationToken.None);

        // Assert
        await Should.ThrowAsync<DbUpdateException>(action);
        DbContext.ChangeTracker.Clear();
        (await DbContext.ForumPostTemplates.SingleAsync()).Id.ShouldBe(template.Id);
        (await DbContext.ForumPostingRules.SingleAsync()).ForumPostTemplateId.ShouldBe(template.Id);
    }

    [Test]
    public void Validate_ValidTemplateBody_ReturnsValidResult()
    {
        // Act
        var result = ForumPostTemplateService.Validate("Hello {{ release.name }}");

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_InvalidTemplateBody_ReturnsErrors()
    {
        // Act
        var result = ForumPostTemplateService.Validate("{{ for x in }}");

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }

    [Test]
    public void Validate_NullTemplateBody_ReturnsValidResult()
    {
        // Act
        var result = ForumPostTemplateService.Validate(null);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    private static ForumPostTemplate CreateTemplate(string name)
    {
        return new ForumPostTemplate
        {
            Name = name,
            Type = ForumPostTemplateType.Release,
            TemplateBody = string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    private static Release CreateRelease(string name)
    {
        return new Release
        {
            Name = name,
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/{name}",
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{name} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };
    }
}
