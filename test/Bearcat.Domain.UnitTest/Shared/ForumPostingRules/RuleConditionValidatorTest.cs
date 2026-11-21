using Bearcat.Domain.Shared.ForumPostingRules;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared.ForumPostingRules;

public class RuleConditionValidatorTest
{
    [Test]
    public void Validate_ValidCondition_ReturnsNoErrors()
    {
        // Arrange
        var condition = RuleConditionFactory.CreateAll(
            RuleConditionFactory.CreateComparison(
                "ContentType",
                RuleConditionOperator.Equals,
                "Movie"
            ),
            RuleConditionFactory.CreateComparison(
                "PrimaryLanguage",
                RuleConditionOperator.IsSet,
                null
            )
        );

        // Act
        var errors = RuleConditionValidator.Validate(condition, RuleFieldCatalog.Fields);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_UnknownField_ReturnsError()
    {
        // Arrange
        var condition = RuleConditionFactory.CreateComparison(
            "Nonsense",
            RuleConditionOperator.Equals,
            "x"
        );

        // Act
        var errors = RuleConditionValidator.Validate(condition, RuleFieldCatalog.Fields);

        // Assert
        errors.ShouldHaveSingleItem().ShouldContain("Unknown field 'Nonsense'");
    }

    [Test]
    public void Validate_OperatorNotAllowedForField_ReturnsError()
    {
        // Arrange
        var condition = RuleConditionFactory.CreateComparison(
            "IsMultiLanguage",
            RuleConditionOperator.Regex,
            "true"
        );

        // Act
        var errors = RuleConditionValidator.Validate(condition, RuleFieldCatalog.Fields);

        // Assert
        errors
            .ShouldHaveSingleItem()
            .ShouldContain("is not allowed for the field 'IsMultiLanguage'");
    }

    [Test]
    public void Validate_MissingValue_ReturnsError()
    {
        // Arrange
        var condition = RuleConditionFactory.CreateComparison(
            "ReleaseName",
            RuleConditionOperator.Like,
            null
        );

        // Act
        var errors = RuleConditionValidator.Validate(condition, RuleFieldCatalog.Fields);

        // Assert
        errors.ShouldHaveSingleItem().ShouldContain("requires a value");
    }

    [Test]
    public void Validate_InOperatorWithoutValues_ReturnsError()
    {
        // Arrange
        var condition = RuleConditionFactory.CreateMultiValueComparison(
            "Resolution",
            RuleConditionOperator.In
        );

        // Act
        var errors = RuleConditionValidator.Validate(condition, RuleFieldCatalog.Fields);

        // Assert
        errors.ShouldHaveSingleItem().ShouldContain("requires at least one value");
    }

    [Test]
    public void Validate_EmptyGroup_ReturnsError()
    {
        // Arrange
        var condition = RuleConditionFactory.CreateAll();

        // Act
        var errors = RuleConditionValidator.Validate(condition, RuleFieldCatalog.Fields);

        // Assert
        errors.ShouldHaveSingleItem().ShouldContain("requires at least one child condition");
    }

    [Test]
    public void Validate_NotWithoutChild_ReturnsError()
    {
        // Arrange
        var condition = new RuleCondition { Kind = RuleConditionKind.Not };

        // Act
        var errors = RuleConditionValidator.Validate(condition, RuleFieldCatalog.Fields);

        // Assert
        errors.ShouldHaveSingleItem().ShouldContain("requires a child condition");
    }

    [Test]
    public void Validate_ComparisonWithChildren_ReturnsError()
    {
        // Arrange
        var condition = RuleConditionFactory.CreateComparison(
            "Year",
            RuleConditionOperator.Equals,
            "2020"
        );
        condition.Children =
        [
            RuleConditionFactory.CreateComparison("Year", RuleConditionOperator.Equals, "2021"),
        ];

        // Act
        var errors = RuleConditionValidator.Validate(condition, RuleFieldCatalog.Fields);

        // Assert
        errors.ShouldHaveSingleItem().ShouldContain("must not contain child conditions");
    }

    [Test]
    public void Validate_InvalidEnumerationValue_ReturnsError()
    {
        // Arrange
        var condition = RuleConditionFactory.CreateMultiValueComparison(
            "Source",
            RuleConditionOperator.In,
            "BluRay",
            "Betamax"
        );

        // Act
        var errors = RuleConditionValidator.Validate(condition, RuleFieldCatalog.Fields);

        // Assert
        errors.ShouldHaveSingleItem().ShouldContain("The value 'Betamax' is not valid");
    }

    [Test]
    public void Validate_NonNumericIntegerValue_ReturnsError()
    {
        // Arrange
        var condition = RuleConditionFactory.CreateComparison(
            "Year",
            RuleConditionOperator.GreaterOrEqual,
            "twenty"
        );

        // Act
        var errors = RuleConditionValidator.Validate(condition, RuleFieldCatalog.Fields);

        // Assert
        errors.ShouldHaveSingleItem().ShouldContain("is not a number");
    }

    [Test]
    public void Validate_NullCondition_ReturnsError()
    {
        // Act
        var errors = RuleConditionValidator.Validate(null, RuleFieldCatalog.Fields);

        // Assert
        errors.ShouldHaveSingleItem().ShouldContain("A rule condition is required.");
    }

    [Test]
    public void ValidateJson_MalformedJson_ReturnsError()
    {
        // Act
        var errors = RuleConditionValidator.ValidateJson("{ broken", RuleFieldCatalog.Fields);

        // Assert
        errors.ShouldHaveSingleItem().ShouldContain("not valid JSON");
    }
}
