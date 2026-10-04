using Bearcat.Domain.UseCases.ManageForumPostTemplates;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageForumPostTemplates;

public class ForumPostTemplateServiceValidateTest
{
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

    [Test]
    public void Validate_SyntaxErrorOnSecondLine_ReturnsOneBasedLineAndColumnWithoutPositionPrefix()
    {
        // Act
        var result = ForumPostTemplateService.Validate("first line\n  {{ end }}");

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldBe([
            new ForumPostTemplateError(
                "Error while parsing ScriptPage: Found <end> statement without a corresponding beginning of a block in: ...",
                Line: 2,
                Column: 8
            ),
        ]);
    }
}
