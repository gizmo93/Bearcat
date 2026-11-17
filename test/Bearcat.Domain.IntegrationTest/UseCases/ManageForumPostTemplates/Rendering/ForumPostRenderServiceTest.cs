using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Rendering;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Rendering.Preview;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Moq;
using Scriban.Runtime;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageForumPostTemplates.Rendering;

public class ForumPostRenderServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private Mock<IForumPostRenderSource> renderSourceMock = null!;
    private ForumPostRenderService service = null!;

    [SetUp]
    public void Setup()
    {
        renderSourceMock = new Mock<IForumPostRenderSource>(MockBehavior.Strict);
        renderSourceMock.SetupGet(source => source.Type).Returns(ForumPostTemplateType.Release);

        service = new ForumPostRenderService(
            new ForumPostTemplateRepository(DbContext, DbContext),
            [renderSourceMock.Object]
        );
    }

    [Test]
    public async Task RenderAsync_TemplateNotFound_ReturnsError()
    {
        // Act
        var result = await service.RenderAsync(1, 999, null, CancellationToken.None);

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
        var result = await service.RenderAsync(1, template.Id, null, CancellationToken.None);

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
            .Setup(source => source.BuildGlobalsAsync(42, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ForumPostRenderGlobals?)null);

        // Act
        var result = await service.RenderAsync(42, template.Id, null, CancellationToken.None);

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
            .Setup(source => source.BuildGlobalsAsync(7, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ForumPostRenderGlobals(globals, BlockedReasons: []));

        // Act
        var result = await service.RenderAsync(7, template.Id, null, CancellationToken.None);

        // Assert
        result.Errors.ShouldBeEmpty();
        result.Content.ShouldBe("Hello Bearcat");
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
            .Setup(source => source.BuildGlobalsAsync(1, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ForumPostRenderGlobals(globals, BlockedReasons: []));

        // Act
        var result = await service.RenderAsync(1, template.Id, null, CancellationToken.None);

        // Assert
        result.Content.ShouldBeEmpty();
        result.Errors.ShouldNotBeEmpty();
    }

    [Test]
    public async Task RenderAsync_TemplateHasParseErrors_ReturnsErrorWithLineAndColumn()
    {
        // Arrange
        var template = await AddTemplateAsync(ForumPostTemplateType.Release, "first\n  {{ end }}");
        renderSourceMock
            .Setup(source => source.BuildGlobalsAsync(1, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ForumPostRenderGlobals(new ScriptObject(), BlockedReasons: []));

        // Act
        var result = await service.RenderAsync(1, template.Id, null, CancellationToken.None);

        // Assert
        result.Errors.ShouldBe([
            "Line 2, column 8: Error while parsing ScriptPage: Found <end> statement without a corresponding beginning of a block in: ...",
        ]);
    }

    [Test]
    public async Task LoadPreviewDataAsync_EntityDoesNotExist_ReturnsNull()
    {
        // Arrange
        renderSourceMock
            .Setup(source => source.BuildGlobalsAsync(404, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ForumPostRenderGlobals?)null);

        // Act
        var result = await service.LoadPreviewDataAsync(
            ForumPostTemplateType.Release,
            404,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task RenderPreviewAsync_ValidTemplate_RendersWithPreviewData()
    {
        // Arrange
        var previewData = await LoadPreviewDataAsync(new ScriptObject { ["name"] = "Bearcat" });

        // Act
        var result = await ForumPostRenderService.RenderPreviewAsync(
            previewData,
            "Hello {{ name }}"
        );

        // Assert
        result.Errors.ShouldBeEmpty();
        result.Content.ShouldBe("Hello Bearcat");
    }

    [Test]
    public async Task RenderPreviewAsync_SyntaxError_ReturnsErrorWithLineAndColumn()
    {
        // Arrange
        var previewData = await LoadPreviewDataAsync(new ScriptObject());

        // Act
        var result = await ForumPostRenderService.RenderPreviewAsync(
            previewData,
            "line one\nline two {{ x = }}"
        );

        // Assert
        result.Content.ShouldBeEmpty();
        result.Errors.ShouldBe([
            new ForumPostTemplateError(
                "Error while parsing assign expression: Expecting <expression> instead of `}}` in: <target_expression> = <value_expression>",
                Line: 2,
                Column: 17
            ),
        ]);
    }

    [Test]
    public async Task RenderPreviewAsync_RuntimeError_ReturnsErrorWithLineAndColumn()
    {
        // Arrange
        var previewData = await LoadPreviewDataAsync(new ScriptObject());

        // Act
        var result = await ForumPostRenderService.RenderPreviewAsync(
            previewData,
            "x\n  {{ [1, 2] | array.foo }}"
        );

        // Assert
        result.Content.ShouldBeEmpty();
        result.Errors.ShouldBe([
            new ForumPostTemplateError(
                "The function `array.foo` was not found",
                Line: 2,
                Column: 15
            ),
        ]);
    }

    [Test]
    public async Task RenderPreviewAsync_TemplateAssignsVariable_DoesNotLeakIntoNextRender()
    {
        // Arrange
        var previewData = await LoadPreviewDataAsync(new ScriptObject { ["name"] = "Bearcat" });
        await ForumPostRenderService.RenderPreviewAsync(
            previewData,
            "{{ x = 1 }}{{ name = 'Changed' }}"
        );

        // Act
        var result = await ForumPostRenderService.RenderPreviewAsync(
            previewData,
            "[{{ x }}]{{ name }}"
        );

        // Assert
        result.Errors.ShouldBeEmpty();
        result.Content.ShouldBe("[]Bearcat");
    }

    [Test]
    public async Task RenderPreviewAsync_TemplateAssignsNestedMember_DoesNotLeakIntoNextRender()
    {
        // Arrange
        var previewData = await LoadPreviewDataAsync(
            new ScriptObject
            {
                ["imagelinks"] = new ScriptObject
                {
                    ["cover"] = new ScriptObject { ["full"] = "https://img.example/full.jpg" },
                },
            }
        );
        await ForumPostRenderService.RenderPreviewAsync(
            previewData,
            "{{ imagelinks.cover.full = 'changed' }}"
        );

        // Act
        var result = await ForumPostRenderService.RenderPreviewAsync(
            previewData,
            "{{ imagelinks.cover.full }}"
        );

        // Assert
        result.Errors.ShouldBeEmpty();
        result.Content.ShouldBe("https://img.example/full.jpg");
    }

    private async Task<ForumPostTemplatePreviewData> LoadPreviewDataAsync(ScriptObject globals)
    {
        renderSourceMock
            .Setup(source => source.BuildGlobalsAsync(1, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ForumPostRenderGlobals(globals, BlockedReasons: []));

        var previewData = await service.LoadPreviewDataAsync(
            ForumPostTemplateType.Release,
            1,
            CancellationToken.None
        );

        return previewData.ShouldNotBeNull();
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

        DbContext.ForumPostTemplates.Add(template);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return template;
    }
}
