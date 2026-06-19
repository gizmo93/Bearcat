using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Rendering;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using Scriban.Runtime;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageForumPostTemplates.Rendering;

public class ForumPostRenderServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private Mock<IForumPostRenderSource> renderSourceMock = null!;
    private ForumPostRenderService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        renderSourceMock = new Mock<IForumPostRenderSource>(MockBehavior.Strict);
        renderSourceMock.SetupGet(source => source.Type).Returns(ForumPostTemplateType.Release);

        service = new ForumPostRenderService(
            new ForumPostTemplateRepository(dbContext, dbContext),
            [renderSourceMock.Object]
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public void GetVariables_KnownType_ReturnsSourceVariables()
    {
        // Arrange
        var variables = new List<ForumPostTemplateVariableReadModel>
        {
            new("{{ release.name }}", "The release name"),
        };
        renderSourceMock.Setup(source => source.GetVariables()).Returns(variables);

        // Act
        var result = service.GetVariables(ForumPostTemplateType.Release);

        // Assert
        result.ShouldBe(variables);
    }

    [Test]
    public void GetVariables_UnknownType_ReturnsEmpty()
    {
        // Act
        var result = service.GetVariables(ForumPostTemplateType.ReleaseCollection);

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task RenderAsync_TemplateNotFound_ReturnsError()
    {
        // Act
        var result = await service.RenderAsync(1, 999, CancellationToken.None);

        // Assert
        result.Content.ShouldBeEmpty();
        result.Errors.ShouldContain("Forum post template not found.");
    }

    [Test]
    public async Task RenderAsync_NoRenderSourceForType_ReturnsError()
    {
        // Arrange
        var template = await AddTemplateAsync(ForumPostTemplateType.ReleaseCollection, "anything");

        // Act
        var result = await service.RenderAsync(1, template.Id, CancellationToken.None);

        // Assert
        result.Content.ShouldBeEmpty();
        result.Errors.ShouldContain(
            $"No render source available for template type {ForumPostTemplateType.ReleaseCollection}."
        );
    }

    [Test]
    public async Task RenderAsync_BuildGlobalsReturnsNull_RendersWithEmptyGlobals()
    {
        // Arrange
        var template = await AddTemplateAsync(ForumPostTemplateType.Release, "Just static text");
        renderSourceMock
            .Setup(source => source.BuildGlobalsAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScriptObject?)null);

        // Act
        var result = await service.RenderAsync(42, template.Id, CancellationToken.None);

        // Assert
        result.Errors.ShouldBeEmpty();
        result.Content.ShouldBe("Just static text");
    }

    [Test]
    public async Task RenderAsync_ValidTemplate_RendersWithGlobals()
    {
        // Arrange
        var template = await AddTemplateAsync(ForumPostTemplateType.Release, "Hello {{ name }}");
        var globals = new ScriptObject { ["name"] = "Bearcat" };
        renderSourceMock
            .Setup(source => source.BuildGlobalsAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(globals);

        // Act
        var result = await service.RenderAsync(7, template.Id, CancellationToken.None);

        // Assert
        result.Errors.ShouldBeEmpty();
        result.Content.ShouldBe("Hello Bearcat");
    }

    [Test]
    public async Task RenderAsync_TemplateHasParseErrors_ReturnsErrors()
    {
        // Arrange
        var template = await AddTemplateAsync(ForumPostTemplateType.Release, "{{ for x in }}");
        renderSourceMock
            .Setup(source => source.BuildGlobalsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScriptObject());

        // Act
        var result = await service.RenderAsync(1, template.Id, CancellationToken.None);

        // Assert
        result.Content.ShouldBeEmpty();
        result.Errors.ShouldNotBeEmpty();
    }

    [Test]
    public async Task RenderAsync_RenderThrowsRuntimeException_ReturnsError()
    {
        // Arrange
        var template = await AddTemplateAsync(ForumPostTemplateType.Release, "{{ fail }}");
        var globals = new ScriptObject();
        globals.Import(
            "fail",
            new Func<string>(() => throw new InvalidOperationException("kaboom"))
        );
        renderSourceMock
            .Setup(source => source.BuildGlobalsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(globals);

        // Act
        var result = await service.RenderAsync(1, template.Id, CancellationToken.None);

        // Assert
        result.Content.ShouldBeEmpty();
        result.Errors.ShouldNotBeEmpty();
    }

    private async Task<ForumPostTemplate> AddTemplateAsync(ForumPostTemplateType type, string body)
    {
        var template = new ForumPostTemplate
        {
            Name = $"Template {Guid.NewGuid():N}",
            Type = type,
            TemplateBody = body,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        dbContext.ForumPostTemplates.Add(template);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        return template;
    }
}
