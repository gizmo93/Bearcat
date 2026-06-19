using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageForumPostTemplates;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageForumPostTemplates;

public class ForumPostTemplateServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private ForumPostTemplateService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        service = new ForumPostTemplateService(
            new ForumPostTemplateRepository(dbContext, dbContext)
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
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
        var template = await dbContext.ForumPostTemplates.SingleAsync();
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
        var template = await dbContext.ForumPostTemplates.SingleAsync(t => t.Id == id);
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
        var template = await dbContext.ForumPostTemplates.SingleAsync(t => t.Id == id);
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
        var template = await dbContext.ForumPostTemplates.SingleAsync(t => t.Id == id);
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
        dbContext.ForumPostTemplates.Add(template);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        await service.DeleteAsync(template.Id, CancellationToken.None);

        // Assert
        (await dbContext.ForumPostTemplates.AnyAsync()).ShouldBeFalse();
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
}
